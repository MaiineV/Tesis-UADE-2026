using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Grid;
using Rollgeon.Tiles.Forced;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>Corre el ciclo de ataque real turno a turno: el cableado lo garantiza
    /// <c>CajeroPhaseWiringTests</c>, acá se verifica que lo que sale es lo prometido. Sin
    /// <c>chip</c> el empujón pega y tira pero no suelta nada, así que el fixture no necesita
    /// hazards ni ledger; el <see cref="IForcedMovementService"/> falso sí, o el tumbo no existe.</summary>
    [TestFixture]
    public class CajeroTurnCycleTests
    {
        private const int RoomSize = 11;

        /// <summary>Manhattan 1 desde <see cref="BossTile"/>: la casilla desde la que se pegan.</summary>
        private static readonly GridCoord GluedTile = new GridCoord(6, 5);

        private static readonly GridCoord BossTile = new GridCoord(5, 5);

        /// <summary>Manhattan 3: fuera del alcance de los dos golpes.</summary>
        private static readonly GridCoord AwayTile = new GridCoord(8, 5);

        /// <summary>Manhattan 5, el standoff: desde acá es artillero.</summary>
        private static readonly GridCoord FarTile = new GridCoord(10, 5);

        private GridManager _grid;
        private SpyDamagePipeline _pipeline;
        private FakeForcedMovementService _forced;
        private ThreatenedAreaService _threat;
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

            // El real y no un fake: el cañonazo marca y cobra contra este servicio, y lo que
            // se verifica acá es justamente el ida y vuelta entre los dos turnos.
            _threat = new ThreatenedAreaService();
            ServiceLocator.AddService<IThreatenedAreaService>(_threat, ServiceScope.Global);

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

        /// <summary>El mandoble abre porque el índice del <c>Alternate</c> arranca en 0: la pelea empieza
        /// con el golpe que no se puede evitar de ninguna manera.</summary>
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

        /// <summary><see cref="AINode_Alternate"/> avanza el índice antes de tickear y no lo devuelve si
        /// el hijo falla: por eso el gate de rango vive afuera.</summary>
        [Test]
        public void AttackCycle_DoesNotBurnASlot_OnTheTurnsHeSpendsWalking()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();

            // Turno 1: lejos. Marca el cañonazo y el ciclo no llega a tickear.
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder alejarse.");
            gate.Tick(NewContext(0));
            Assert.IsEmpty(Damages(), "Pegó a distancia 3: los dos golpes son de contacto.");

            // Turno 2: vuelve a pegarse. Cobrar la marca es el ataque del turno y falla porque el
            // jugador ya no está en el área — pero tampoco toca el ciclo.
            Assert.IsTrue(_grid.Move(_player, GluedTile), "Fixture: el jugador tiene que poder volver.");
            gate.Tick(NewContext(1));
            Assert.IsEmpty(Damages(), "Se salió del área marcada: el cañonazo no le cobra nada.");

            gate.Tick(NewContext(2));

            CollectionAssert.AreEqual(new[] { CajeroAssetBuilder.HeavyDamage }, Damages(),
                "Los turnos sin golpe le gastaron un lugar del ciclo: el primer golpe que conecta " +
                "salió empujón en vez de mandoble. El If de rango tiene que quedar POR FUERA del " +
                "Alternate, y el del cañonazo por fuera de los dos.");
        }

        /// <summary>El agujero que cierra el cañonazo: caminar y no llegar era un turno en el que no
        /// pasaba absolutamente nada.</summary>
        [Test]
        public void OutOfReach_HeMarksAndCollectsIt_WhenThePlayerStaysPut()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder alejarse.");

            gate.Tick(NewContext(0));
            Assert.IsEmpty(Damages(), "El aviso no pega: el jugador tiene su turno para salirse.");

            gate.Tick(NewContext(1));

            CollectionAssert.AreEqual(new[] { CajeroAssetBuilder.SlamDamage }, Damages(),
                "Se quedó parado en el área marcada y no le cobró nada: el turno de caminata vuelve " +
                "a ser un turno perdido.");
        }

        /// <summary>Lejos es artillero: el turno que cobra deja la siguiente marca puesta, así que
        /// kitearlo no lo apaga — cada turno hay un 3×3 del que salirse.</summary>
        [Test]
        public void FarAway_HeFiresAndReloadsInTheSameTurn()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            Assert.IsTrue(_grid.Move(_player, FarTile), "Fixture: el jugador tiene que poder irse al standoff.");
            gate.Tick(NewContext(0));

            gate.Tick(NewContext(1));

            CollectionAssert.AreEqual(new[] { CajeroAssetBuilder.SlamDamage }, Damages(),
                "Se quedó en el área marcada y no le cobró.");
            Assert.IsTrue(_threat.TryPeek(_boss, out _),
                "Disparó y no recargó: el artillero tira una vez y se queda mirando.");
        }

        /// <summary>Si te acercaste por debajo del standoff cobra lo marcado y no recarga: el turno que
        /// viene ya es de los golpes de contacto.</summary>
        [Test]
        public void CloseIn_HeFiresWithoutReloading()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            Assert.IsTrue(_grid.Move(_player, FarTile), "Fixture: el jugador tiene que poder irse al standoff.");
            gate.Tick(NewContext(0));

            // A tres del jefe: dentro del 3×3 marcado (corrido hacia adentro contra la pared) y por
            // debajo del standoff.
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder acercarse.");
            gate.Tick(NewContext(1));

            CollectionAssert.AreEqual(new[] { CajeroAssetBuilder.SlamDamage }, Damages(),
                "Entró al área marcada y no le cobró.");
            Assert.IsFalse(_threat.TryPeek(_boss, out _),
                "Recargó con el jugador cerca: el cañón le come el turno a los golpes.");
        }

        /// <summary>El bug de playtest: contra la pared el cuadrado salía mordido —en una esquina
        /// llegaba a 3 casillas— y no había forma de leer qué estaba amenazado. <b>Esto sólo se
        /// reproduce con un grafo real</b>: con el NavGraph vacío de la mayoría de los fixtures
        /// <c>HasNode</c> contesta true a todo y no recorta nada.</summary>
        [Test]
        public void TheSlam_MarksAWholeSquare_EvenWithThePlayerAgainstTheWall()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            var corner = new GridCoord(0, 0);
            Assert.IsTrue(_grid.Move(_player, corner), "Fixture: el jugador tiene que llegar a la esquina.");

            gate.Tick(NewContext(0));

            Assert.IsTrue(_threat.TryPeek(_boss, out var area), "No marcó nada estando en la esquina.");

            int whole = (2 * CajeroAssetBuilder.SlamRadius + 1) * (2 * CajeroAssetBuilder.SlamRadius + 1);
            Assert.AreEqual(whole, area.Tiles.Count,
                "El cuadrado salió mordido contra la pared en vez de correrse hacia adentro.");
            CollectionAssert.Contains(area.Tiles.ToList(), corner,
                "Se corrió tanto que el jugador quedó fuera del área que se centra en él.");
        }

        /// <summary>Un cañonazo arquea. Con el filtro de visión puesto, lo que se interponga le come
        /// casillas al cuadrado —y en el 13.7% de las posiciones lo dejaba vacío, o sea el jefe
        /// perdía el turno de marca sin que nada lo explicara.</summary>
        [Test]
        public void TheSlam_IgnoresWhatBlocksHisView_BecauseItArcs()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder alejarse.");

            // Justo en la línea entre los dos, como una caja fuerte de la sala real.
            _grid.Register(Guid.NewGuid(), new GridCoord(BossTile.X + 1, BossTile.Y));

            gate.Tick(NewContext(0));

            Assert.IsTrue(_threat.TryPeek(_boss, out var area), "No marcó nada con algo en el medio.");

            int whole = (2 * CajeroAssetBuilder.SlamRadius + 1) * (2 * CajeroAssetBuilder.SlamRadius + 1);
            Assert.AreEqual(whole, area.Tiles.Count,
                "Volvió el recorte por línea de visión: el cuadrado se lee roto y a veces no marca nada.");
        }

        /// <summary>Salirse del 3×3 lo anula entero: no hay daño reducido por quedar al borde.</summary>
        [Test]
        public void TheSlam_MissesCompletely_WhenThePlayerStepsOutOfTheMarkedArea()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder alejarse.");
            gate.Tick(NewContext(0));

            // Cuatro filas arriba: fuera del 3×3 centrado en AwayTile y fuera de contacto.
            Assert.IsTrue(_grid.Move(_player, new GridCoord(AwayTile.X, AwayTile.Y - 4)),
                "Fixture: el jugador tiene que poder esquivar.");
            gate.Tick(NewContext(1));

            Assert.IsEmpty(Damages(),
                "Cobró fuera del área: el aviso deja de ser una decisión y pasa a ser un impuesto.");
        }

        /// <summary>Un <c>Failed</c> acá abortaría el Sequence del turno y se llevaría las monedas, la
        /// caja y la persecución: el jefe se quedaría clavado justo en los turnos que camina. Ahora
        /// el Succeeded sale de haber marcado el cañonazo, no de un <c>Wait</c>.</summary>
        [Test]
        public void AttackGate_SucceedsOutOfRange_SoTheRestOfTheTurnStillRuns()
        {
            var gate = CajeroAssetBuilder.BuildAttackGate();
            Assert.IsTrue(_grid.Move(_player, AwayTile), "Fixture: el jugador tiene que poder alejarse.");

            Assert.AreEqual(AIResult.Succeeded, gate.Tick(NewContext(0)),
                "El gate del ataque falló con el jugador lejos, que es su caso más común.");
        }

        /// <summary>Con <c>Range = 1</c> y métrica Manhattan el jugador está siempre ortogonalmente
        /// pegado, así que el cardinal es exacto y no hay desempate que elegir.</summary>
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

        /// <summary>Registra los empujones pedidos sin mover nada: el tumbo real es del servicio y ya
        /// tiene sus tests, acá sólo importa qué le pide el nodo.</summary>
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
