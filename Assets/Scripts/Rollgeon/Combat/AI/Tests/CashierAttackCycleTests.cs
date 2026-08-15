using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Economy;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El ciclo de dos turnos del Cajero: un turno marca columna, el siguiente dispara, y así
    /// siempre. Se arma el mismo <c>Alternate</c> que autora <c>CajeroAssetBuilder.BuildAttackCycle</c>
    /// — el builder vive en un assembly de Editor que este de tests no referencia, así que acá se
    /// afirma el <b>comportamiento</b> y en <c>CajeroPhaseWiringTests</c> el cableado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La alternancia es lo que hace que el jefe pueda pegar. Sólo con columna, el jugador daba un
    /// paso al costado y no cobraba nunca. Con el disparo en el medio, salir del área sigue siendo
    /// gratis pero <b>acercarse a pegarle</b> cuesta 12 fijos: para golpear al jefe hay que estar a
    /// distancia 1, y distancia 1 está dentro del rango del disparo.
    /// </para>
    /// <para>
    /// Que sea estricta (y no un <c>Random</c>) es legibilidad: el jugador tiene que poder plantar
    /// el movimiento sabiendo que el turno después de la marca no hay marca nueva.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class CashierAttackCycleTests
    {
        private const int RoomSize = 9;
        private const int PoorTierDamage = 14;
        private const int ShotDamage = 12;

        private static readonly GridCoord PlayerStart = new GridCoord(4, 4);
        private static readonly GridCoord BossStart = new GridCoord(8, 4);

        private GridManager _grid;
        private ThreatenedAreaService _threat;
        private HazardService _hazards;
        private CashierLedgerService _ledger;
        private SpyDamagePipeline _pipeline;
        private HazardDefinitionSO _chip;

        private AINode_Alternate _cycle;
        private AINode_CashierDropChips _chipDrop;

        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSize, RoomSize));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _hazards = new HazardService();
            _hazards.Register();

            // Jugador sin oro: la columna sale en su escalón más barato (14, ancho 1), que es el
            // número contra el que se afirman los golpes de abajo.
            ServiceLocator.AddService<IEconomyService>(new FakeEconomyService());

            _ledger = new CashierLedgerService();
            ServiceLocator.AddService<ICashierLedgerService>(_ledger);

            _pipeline = new SpyDamagePipeline();

            _player = Guid.NewGuid();
            _boss = Guid.NewGuid();
            _grid.Register(_player, PlayerStart);
            _grid.Register(_boss, BossStart);

            _chip = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _chip.hideFlags = HideFlags.HideAndDontSave;
            _chip.Trigger = HazardTriggerMode.OnEnter;
            _chip.ConsumeOnTrigger = true;
            _chip.Damage = 0;
            _chip.DurationRounds = 1;
            _chip.SourceId = Guid.NewGuid().ToString();

            _cycle = BuildAttackCycle();
            _chipDrop = BuildChipDrop();
        }

        [TearDown]
        public void TearDown()
        {
            _ledger.Dispose();
            _hazards.Dispose();

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            UnityEngine.Object.DestroyImmediate(_chip);
            TypedEvent<DamageResolvedPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ---- El árbol, igual que el builder ------------------------------

        private static AINode_Selector WrapFallible(AIDecisionNode child) => new AINode_Selector
        {
            Children = new List<AIDecisionNode> { child, new AINode_Wait() },
        };

        private static AINode_Alternate BuildAttackCycle() => new AINode_Alternate
        {
            Children = new List<AIDecisionNode>
            {
                WrapFallible(new AINode_TelegraphMarkGoldScaled
                {
                    Shape = ThreatShape.Column,
                    Tiers = CashierFicha.Tiers(),
                }),
                WrapFallible(new AINode_CashierRangedShot
                {
                    Damage = ShotDamage,
                    Range = 4,
                    Metric = DistanceMetric.Manhattan,
                }),
            },
        };

        private AINode_CashierDropChips BuildChipDrop() => new AINode_CashierDropChips
        {
            Chip = _chip,
            Count = 1,
            MinValue = 6,
            MaxValue = 9,
            MinDistanceFromPlayer = 2,
            MaxDistanceFromPlayer = 3,
            RequireDamageTaken = true,
        };

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            DamagePipeline = _pipeline,
            SelfMaxHp = 190,
            Rng = new System.Random(3),
        };

        /// <summary>
        /// Un turno del jefe con los tres hijos que le importan al ciclo: detona lo del turno
        /// pasado, ataca (columna o disparo) y suelta ficha. Se saltean el gate del arqueo y el
        /// repliegue: no participan de la alternancia.
        /// </summary>
        private void RunBossTurn()
        {
            var context = NewContext();
            new AINode_ExecuteTelegraph().Tick(context);
            _cycle.Tick(context);
            _chipDrop.Tick(context);
        }

        private void PlayerHitsTheBoss() =>
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _player,
                TargetGuid = _boss,
                FinalDamage = 20,
            });

        private int LiveChips()
        {
            var live = new List<HazardInstanceInfo>();
            foreach (var info in _hazards.ActiveInstances()) live.Add(info);
            return live.Count;
        }

        // =====================================================================
        // Alternancia
        // =====================================================================

        [Test]
        public void test_attackCycle_opensByMarking_notByShooting()
        {
            // Act
            RunBossTurn();

            // Assert
            Assert.IsTrue(_threat.HasPending(_boss), "El primer turno del jefe marca la columna.");
            Assert.IsEmpty(_pipeline.Resolved,
                "Abrir disparando serían 12 antes de que el jugador haya visto de qué va la pelea.");
        }

        [Test]
        public void test_attackCycle_secondTurn_shootsAndMarksNothing()
        {
            // Arrange
            RunBossTurn();

            // Act
            RunBossTurn();

            // Assert
            Assert.IsFalse(_threat.HasPending(_boss),
                "El turno de disparo no marca: el jugador tiene un turno entero de aire para leer la sala.");
            Assert.AreEqual(2, _pipeline.Resolved.Count, "Detona la columna del turno pasado y además dispara.");
            Assert.AreEqual(PoorTierDamage, _pipeline.Resolved[0].BaseDamage, "Primero cobra la columna…");
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[1].BaseDamage, "…y después tira la ficha.");
        }

        [Test]
        public void test_attackCycle_thirdTurn_marksAgain_soTheRhythmIsStrict()
        {
            // Arrange
            RunBossTurn();
            RunBossTurn();

            // Act
            RunBossTurn();

            // Assert
            Assert.IsTrue(_threat.HasPending(_boss), "Marca, dispara, marca: sin excepciones ni azar.");
            Assert.AreEqual(2, _pipeline.Resolved.Count,
                "El turno de marca no suma daño: la columna que detonó ya se cobró en el turno de " +
                "disparo, y la nueva recién se cobra en el siguiente.");
        }

        [Test]
        public void test_attackCycle_dodgingTheColumn_stillEatsTheShot()
        {
            // Arrange — el jugador hace lo único que la columna sola le pedía: dar un paso al lado.
            RunBossTurn();
            Assert.IsTrue(_grid.Move(_player, new GridCoord(PlayerStart.X + 1, PlayerStart.Y)));

            // Act
            RunBossTurn();

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count, "La columna falla, como corresponde…");
            Assert.AreEqual(ShotDamage, _pipeline.Resolved[0].BaseDamage,
                "…pero esquivar ya no es salir gratis: el disparo no se esquiva moviéndose de columna.");
        }

        [Test]
        public void test_attackCycle_playerFarFromTheBoss_takesNothing_butCannotAttackEither()
        {
            // Arrange — esquina opuesta: a 12 de Manhattan del jefe, muy fuera del disparo.
            Assert.IsTrue(_grid.Move(_player, new GridCoord(0, 0)));
            RunBossTurn();
            Assert.IsTrue(_grid.Move(_player, new GridCoord(1, 0)), "…y encima sale de su propia columna.");

            // Act
            RunBossTurn();

            // Assert
            Assert.IsEmpty(_pipeline.Resolved,
                "Huir del todo es seguro, y por eso mismo estéril: desde ahí tampoco le pega al jefe.");
        }

        // =====================================================================
        // Las fichas contra la alternancia
        // =====================================================================

        [Test]
        public void test_chips_hitLandedOnAShootingTurn_isPaidOnTheNextMarkingTurn()
        {
            // Arrange — turno 1 marca sin que le hayan pegado todavía.
            RunBossTurn();
            Assert.AreEqual(0, LiveChips());

            // Act — el jugador le pega y el jefe contesta con el turno de disparo (sin columna).
            PlayerHitsTheBoss();
            RunBossTurn();
            Assert.AreEqual(0, LiveChips(), "En el turno de disparo no hay columna donde soltarla.");

            RunBossTurn();

            // Assert
            Assert.AreEqual(1, LiveChips(),
                "El flag de daño no se consume en los turnos sin columna: si se consumiera, una de " +
                "cada dos fichas desaparecería sin llegar al piso.");
        }

        [Test]
        public void test_chips_dropOnTheSameTurnWhenTheHitLandsBeforeAMarkingTurn()
        {
            // Arrange
            PlayerHitsTheBoss();

            // Act
            RunBossTurn();

            // Assert
            Assert.AreEqual(1, LiveChips(), "Turno de columna con golpe pendiente: la ficha cae ya.");
        }

        [Test]
        public void test_chips_oneHitPaysExactlyOneChip_acrossTheWholeCycle()
        {
            // Arrange
            PlayerHitsTheBoss();

            // Act — marca (paga), dispara, marca de nuevo sin golpes nuevos.
            RunBossTurn();
            RunBossTurn();
            RunBossTurn();

            // Assert
            Assert.AreEqual(1, LiveChips(), "Un golpe, una ficha — el flag se consume al pagarla.");
        }

        private sealed class SpyDamagePipeline : IDamagePipeline
        {
            public readonly List<DamageContext> Resolved = new List<DamageContext>();

            public DamageContext Resolve(DamageContext ctx)
            {
                Resolved.Add(ctx);
                return ctx;
            }

            public DamageContext Preview(DamageContext ctx) => ctx;
        }
    }
}
