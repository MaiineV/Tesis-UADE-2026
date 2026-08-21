using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Grid;
using Rollgeon.Tiles.Forced;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Corre el ciclo de ataque <b>real</b> del Cajero turno a turno. Lo que cubre es la
    /// alternancia estricta de sus dos golpes: mandoble, empujón, mandoble, empujón — y que los
    /// turnos que pasa caminando <b>no</b> le gasten un lugar del ciclo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La alternancia es media mecánica: el empujón es el único golpe que el jugador puede preparar
    /// (eligiendo desde qué casilla atacarlo, para que el tumbo no lo tire contra los pinchos), y eso
    /// sólo funciona si puede contar los turnos. El cableado que lo garantiza vive en
    /// <c>CajeroPhaseWiringTests</c>; acá se verifica que lo que sale es lo prometido.
    /// </para>
    /// <para>
    /// Sin <c>chip</c>: con la definición de moneda en null el empujón pega y tira, pero no suelta
    /// nada, así que el fixture no necesita hazards ni el ledger. Lo que sí se registra es un
    /// <see cref="IForcedMovementService"/> falso — sin él el nodo loguea un warning y el tumbo no
    /// existe, y de paso sirve para contar qué turnos empujaron de verdad.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CajeroTurnCycleTests
    {
        private const int RoomSize = 11;

        /// <summary>Manhattan 1 desde <see cref="BossTile"/>: la casilla desde la que se pegan.</summary>
        private static readonly GridCoord GluedTile = new GridCoord(6, 5);

        private static readonly GridCoord BossTile = new GridCoord(5, 5);

        /// <summary>Manhattan 3: fuera del alcance de los dos golpes.</summary>
        private static readonly GridCoord AwayTile = new GridCoord(8, 5);

        private GridManager _grid;
        private SpyDamagePipeline _pipeline;
        private FakeForcedMovementService _forced;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSize, RoomSize));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline, ServiceScope.Global);

            _forced = new FakeForcedMovementService();
            ServiceLocator.AddService<IForcedMovementService>(_forced, ServiceScope.Global);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
            _grid.Register(_boss, BossTile);
            _grid.Register(_player, GluedTile);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ---- La alternancia ------------------------------------------------

        /// <summary>
        /// Cuatro turnos pegados: mandoble, empujón, mandoble, empujón. El mandoble abre porque el
        /// índice del <c>Alternate</c> arranca en 0 — la pelea tiene que empezar con el golpe que no
        /// se puede evitar de ninguna manera.
        /// </summary>
        [Test]
        public void AttackCycle_AlternatesStrictly_WhenEveryTurnConnects()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();

            for (int turn = 0; turn < 4; turn++) gate.Tick(NewContext(turn));

            CollectionAssert.AreEqual(
                new[]
                {
                    CajeroAssetBuilder.HeavyDamage,
                    CajeroAssetBuilder.ShoveDamage,
                    CajeroAssetBuilder.HeavyDamage,
                    CajeroAssetBuilder.ShoveDamage,
                },
                Damages(),
                "El ciclo dejó de alternar. AINode_Alternate rota por índice, así que dos golpes " +
                "iguales seguidos significan que algo le está moviendo el índice de más (o de menos) " +
                "y el jugador ya no puede preparar el turno del empujón.");

            Assert.AreEqual(2, _forced.Pushes.Count,
                "Empujó una cantidad de veces distinta a la mitad de los turnos: o el mandoble " +
                "también tira, o el empujón dejó de tirar.");
        }

        /// <summary>
        /// El gate de rango vive <b>afuera</b> del <c>Alternate</c>, y ésta es la razón:
        /// <see cref="AINode_Alternate"/> avanza el índice antes de tickear y no lo devuelve si el
        /// hijo falla. Con los golpes auto-gateándose solos, un turno de caminata quemaría un lugar
        /// del ciclo y el jugador contaría mandoble-empujón mientras le llegan dos mandobles.
        /// </summary>
        [Test]
        public void AttackCycle_DoesNotBurnASlot_OnTheTurnsHeSpendsWalking()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();

            // Turno 1: lejos. El gate cae al Else (Wait) y el ciclo no llega a tickear.
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder alejarse.");
            gate.Tick(NewContext(0));
            Assert.IsEmpty(Damages(), "Pegó a distancia 3: los dos golpes son de contacto.");

            // Turno 2: vuelve a estar pegado.
            Assert.IsTrue(_grid.Move(_player, GluedTile), "Fixture: el jugador tiene que poder volver.");
            gate.Tick(NewContext(1));

            CollectionAssert.AreEqual(new[] { CajeroAssetBuilder.HeavyDamage }, Damages(),
                "El turno de caminata le gastó un lugar del ciclo: el primer golpe que conecta salió " +
                "empujón en vez de mandoble. El If de rango tiene que quedar POR FUERA del Alternate.");
        }

        /// <summary>
        /// Fuera de rango el gate devuelve lo que devuelva su <c>Else</c>, y ése es un
        /// <c>AINode_Wait</c>: un Failed acá abortaría el Sequence del turno y se llevaría puestas
        /// las monedas de la sala, la caja y la persecución — o sea, el jefe se quedaría clavado
        /// justo en los turnos en que tenía que caminar hasta el jugador.
        /// </summary>
        [Test]
        public void AttackGate_SucceedsOutOfRange_SoTheRestOfTheTurnStillRuns()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder alejarse.");

            Assert.AreEqual(AIResult.Succeeded, gate.Tick(NewContext(0)),
                "El gate del ataque falló con el jugador lejos, que es su caso más común.");
        }

        /// <summary>
        /// El empujón lo manda para el lado opuesto al jefe y las casillas de la ficha. Con
        /// <c>Range = 1</c> y métrica Manhattan el jugador está siempre ortogonalmente pegado, así
        /// que el cardinal es exacto y no hay desempate que elegir.
        /// </summary>
        [Test]
        public void Shove_PushesAwayFromTheBoss_ForTheSheetsTiles()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();

            gate.Tick(NewContext(0)); // Mandoble.
            gate.Tick(NewContext(1)); // Empujón.

            Assert.AreEqual(1, _forced.Pushes.Count, "El empujón no llamó al servicio de tumbo.");
            var push = _forced.Pushes[0];

            Assert.AreEqual(_player, push.Entity, "Se empujó a otro.");
            Assert.AreEqual(CajeroAssetBuilder.ShovePushTiles, push.Tiles,
                "Las casillas del tumbo salen de la ficha, no del default del nodo.");
            Assert.AreEqual(_boss, push.SourceId, "El tumbo tiene que quedar atribuido al jefe.");
            Assert.AreEqual(Cardinal.East, push.Direction,
                "El jugador está al ESTE del jefe, así que el tumbo va al este. Invertido, el " +
                "empujón lo trae de vuelta encima del jefe y el tumbo deja de alejarlo de nada.");
        }

        // ---- Helpers -------------------------------------------------------

        private AIContext NewContext(int roundIndex) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
            SelfMaxHp = CajeroAssetBuilder.BaseHP,
            RoundIndex = roundIndex,
            Rng = new System.Random(7),
        };

        private List<int> Damages()
        {
            var damages = new List<int>(_pipeline.Resolved.Count);
            foreach (var hit in _pipeline.Resolved) damages.Add(hit.BaseDamage);
            return damages;
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx)
            {
                ctx.FinalDamage = ctx.BaseDamage;
                return ctx;
            }
        }

        /// <summary>
        /// Registra los empujones pedidos sin mover nada. El tumbo real (frenar contra una caja
        /// fuerte, cobrar los pinchos del camino, las continuaciones de hielo y portal) es del
        /// servicio y ya tiene sus propios tests: acá sólo importa qué le pide el nodo.
        /// </summary>
        private sealed class FakeForcedMovementService : IForcedMovementService
        {
            public readonly List<PushCall> Pushes = new List<PushCall>();

            public ForcedMoveResult Push(Guid entity, Cardinal direction, int tiles, Guid sourceId)
            {
                Pushes.Add(new PushCall(entity, direction, tiles, sourceId));

                // Tumbo completo y vivo: es el caso en que el nodo sigue hasta soltar monedas, o sea
                // el camino más largo del código bajo prueba.
                return new ForcedMoveResult(
                    default(GridCoord), tiles, ForcedMoveStop.CompletedDistance, false);
            }

            public readonly struct PushCall
            {
                public readonly Guid Entity;
                public readonly Cardinal Direction;
                public readonly int Tiles;
                public readonly Guid SourceId;

                public PushCall(Guid entity, Cardinal direction, int tiles, Guid sourceId)
                {
                    Entity = entity;
                    Direction = direction;
                    Tiles = tiles;
                    SourceId = sourceId;
                }
            }
        }
    }
}
