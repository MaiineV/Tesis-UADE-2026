using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Ciclo completo de La Bandida a nivel nodo: cuenta → disparo → rearme inmediato,
    /// cancelación por daño en un rodillo, y la pausa de la reposición por fase.
    /// </summary>
    /// <remarks>
    /// El árbol se arma acá a mano (sin el builder, que vive en el assembly de Editor) con los
    /// mismos nodos y el mismo orden que <c>BandidaAssetBuilder.BuildAIRoot</c>: tick de la cuenta,
    /// fila de rodillos, y pool con el gate del jackpot. El <see cref="ProbeNode"/> ocupa el lugar
    /// del <c>TelegraphMark</c> del jackpot para no necesitar el servicio de amenazas ni overlays.
    /// </remarks>
    [TestFixture]
    public class BandidaJackpotCycleTests
    {
        private const int CountdownStart = 2;
        private const int RespawnDelayPhase1 = 2;
        private const int RespawnDelayPhase2 = 1;

        private GridManager _grid;
        private AttributesManager _attributes;
        private InMemoryEntityRegistry _registry;
        private TurnOrderService _turnOrder;
        private BandidaJackpotService _service;
        private EnemyDataSO _reelData;

        private Guid _boss;
        private Guid _player;

        private AINode_TickJackpot _tick;
        private AINode_SpawnReels _reels;
        private AINode_Selector _pool;
        private ProbeNode _jackpotProbe;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<JackpotCountdownPayload>.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 7));
            _attributes = new AttributesManager();
            _registry = new InMemoryEntityRegistry();
            _turnOrder = new TurnOrderService();
            ServiceLocator.AddService<InMemoryEntityRegistry>(_registry);
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);

            _service = new BandidaJackpotService();
            _service.Register();

            _reelData = ScriptableObject.CreateInstance<EnemyDataSO>();
            _reelData.BaseHP = 3;
            _reelData.BaseAttack = 0;
            _reelData.AIRoot = new AINode_Wait();

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

            // Atornillada a la pared de arriba: la fila cae en la línea de abajo, alineada.
            _grid.Register(_boss, new GridCoord(5, 6));
            _grid.Register(_player, new GridCoord(1, 1));
            _attributes.Register(_boss, NewStats(140));
            _turnOrder.RestoreState(new[] { _player, _boss }, cursor: 1, roundIndex: 0);

            BuildTree();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<JackpotCountdownPayload>.Clear();
            _attributes.Dispose();
            if (_reelData != null) UnityEngine.Object.DestroyImmediate(_reelData);
        }

        // ======================================================================
        // Fila de rodillos
        // ======================================================================

        [Test]
        public void FirstBossTurn_SpawnsThreeAlignedReels_AndArmsTheCountAtTwo()
        {
            BossTurn();

            var coords = ReelCoords();
            Assert.AreEqual(3, coords.Count, "Tres rodillos en la fila.");
            Assert.AreEqual(1, DistinctYs(coords).Count, "Los tres rodillos tienen que estar alineados.");
            CollectionAssert.AreEquivalent(new[] { 4, 5, 6 }, coords.ConvertAll(c => c.X),
                "La fila arranca en el anillo del jefe, centrada en su columna.");

            // El tick corre ANTES de la fila: el turno del spawn deja la cuenta en 2 completos.
            Assert.IsTrue(_service.IsCounting);
            Assert.AreEqual(CountdownStart, _service.Countdown);
        }

        [Test]
        public void ReelRow_StaysPut_WhileNoReelIsBroken()
        {
            BossTurn();
            var first = ReelCoords();

            BossTurn();
            BossTurn();

            CollectionAssert.AreEquivalent(first, ReelCoords(),
                "Sin roturas no se repone nada: la fila es la misma.");
        }

        // ======================================================================
        // Cuenta → disparo → rearme
        // ======================================================================

        [Test]
        public void Countdown_TicksTwoThenOne_AndFiresTheJackpotOnZero()
        {
            BossTurn(); // Arma en 2 (spawn de la fila).
            Assert.AreEqual(2, _service.Countdown);

            BossTurn();
            Assert.AreEqual(1, _service.Countdown, "Segundo turno del jefe: la cuenta muestra 1.");
            Assert.AreEqual(0, _jackpotProbe.Fired, "Todavía no dispara.");

            BossTurn();
            Assert.AreEqual(1, _jackpotProbe.Fired, "En 0 marca el jackpot.");
        }

        [Test]
        public void JackpotThatFires_RearmsInPlace_NoDeadRoundForTankingIt()
        {
            BossTurn();
            BossTurn();
            BossTurn(); // Dispara.
            Assert.AreEqual(1, _jackpotProbe.Fired);
            Assert.IsTrue(_service.IsCounting, "La cuenta que dispara se rearma en el acto.");
            Assert.AreEqual(CountdownStart, _service.Countdown);

            // Sin pausa: dos turnos más y vuelve a disparar. Tanquear el jackpot no compra tiempo.
            BossTurn();
            Assert.AreEqual(1, _jackpotProbe.Fired);
            BossTurn();
            Assert.AreEqual(2, _jackpotProbe.Fired,
                "El ciclo del jackpot es de dos turnos fijos si el jugador no rompe nada.");
        }

        // ======================================================================
        // Cancelación (hook de daño, no chequeo de HP)
        // ======================================================================

        [Test]
        public void DamagingAnyReel_CancelsTheCount_ThroughTheDamageHook()
        {
            BossTurn();
            var reel = FirstReelGuid();

            // Un solo punto de daño alcanza: la cancelación es el hook, no una comparación de vidas.
            RaiseDamage(reel, 1);

            Assert.IsFalse(_service.IsCounting, "Romper un rodillo cancela la cuenta.");
        }

        [Test]
        public void CancelledCount_DoesNotTick_AndTheJackpotNeverFires()
        {
            BossTurn();
            BreakReel(FirstReelGuid());

            int frozen = _service.Countdown;
            BossTurn();
            BossTurn();

            Assert.AreEqual(0, _jackpotProbe.Fired,
                "Con la cuenta cancelada el jackpot no puede dispararse.");
            Assert.LessOrEqual(_service.Countdown, frozen);
        }

        [Test]
        public void CountFrozenAtZero_DoesNotFire_BecauseThePcRequiresCounting()
        {
            BossTurn();

            // Peor caso de la cancelación: la cuenta ya estaba en el borde del disparo.
            _service.ResetCountdown(0);
            BreakReel(FirstReelGuid());
            Assert.IsFalse(_service.IsCounting);
            Assert.AreEqual(0, _service.Countdown, "El número queda congelado donde estaba.");

            BossTurn();

            Assert.AreEqual(0, _jackpotProbe.Fired,
                "RequireCounting es lo único que impide que un 0 congelado dispare el jackpot " +
                "después de que el jugador desarmó la bomba.");
        }

        [Test]
        public void HitFullyAbsorbedByShield_DoesNotCancel()
        {
            BossTurn();

            RaiseDamage(FirstReelGuid(), 0);

            Assert.IsTrue(_service.IsCounting, "Un golpe que no hizo daño no rompe nada.");
        }

        [Test]
        public void DamageToTheBoss_DoesNotCancel()
        {
            BossTurn();

            RaiseDamage(_boss, 20);

            Assert.IsTrue(_service.IsCounting,
                "Bajarle vida al jefe no desarma la bomba: eso es justo el trade del diseño.");
        }

        // ======================================================================
        // Reposición: la pausa que compra cancelar
        // ======================================================================

        [Test]
        public void BrokenReel_ReturnsOnTheSecondBossTurn_Aligned_AndRearmsTheCountAtTwo()
        {
            BossTurn();
            var reel = FirstReelGuid();
            var slotCoord = CoordOf(reel);
            BreakReel(reel);

            BossTurn();
            Assert.AreEqual(2, ReelCoords().Count, "Turno 1 de espera: el rodillo no volvió todavía.");
            Assert.IsFalse(_service.IsCounting, "La pausa dura lo que dura la reposición.");

            BossTurn();
            Assert.AreEqual(3, ReelCoords().Count, "Fase 1: el rodillo vuelve a los dos turnos.");
            CollectionAssert.Contains(ReelCoords(), slotCoord,
                "Vuelve alineado: a la misma ranura de la que lo rompieron.");
            Assert.IsTrue(_service.IsCounting);
            Assert.AreEqual(CountdownStart, _service.Countdown,
                "Reponer y devolver la cuenta a 2 son el mismo paso.");
        }

        [Test]
        public void BreakingAReel_BuysFourBossTurnsBeforeTheNextJackpot()
        {
            BossTurn();
            BreakReel(FirstReelGuid());

            // 2 turnos de reposición + 2 de cuenta nueva.
            BossTurn();
            BossTurn(); // Vuelve el rodillo, cuenta en 2.
            BossTurn(); // 1
            Assert.AreEqual(0, _jackpotProbe.Fired);
            BossTurn(); // 0 ⇒ marca.
            Assert.AreEqual(1, _jackpotProbe.Fired);
        }

        /// <summary>
        /// El accidente que fija el orden del árbol: si <c>TickJackpot</c> corriera DESPUÉS de la
        /// fila, el turno en que el rodillo vuelve rearmaría la cuenta en 2 y el mismo tick la
        /// bajaría a 1 — el jugador perdería una de las dos rondas de aviso que compró rompiéndolo.
        /// </summary>
        [Test]
        public void RespawnTurn_KeepsBothWarningRounds_TheSameTurnTickDoesNotEatTheRearm()
        {
            BossTurn();
            BreakReel(FirstReelGuid());

            BossTurn(); // Turno de espera.
            BossTurn(); // Vuelve el rodillo.

            Assert.AreEqual(3, ReelCoords().Count);
            Assert.AreEqual(CountdownStart, _service.Countdown,
                "El tick del turno del respawn no puede comerse el rearme: la cuenta arranca en 2, " +
                "no en 1.");

            BossTurn();
            Assert.AreEqual(CountdownStart - 1, _service.Countdown, "Recién acá baja a 1.");
            Assert.AreEqual(0, _jackpotProbe.Fired);

            BossTurn();
            Assert.AreEqual(1, _jackpotProbe.Fired,
                "Dos rondas de aviso completas después del respawn, y ahí sí el jackpot.");
        }

        [Test]
        public void InPhaseTwo_BrokenReelReturnsOnTheFirstBossTurn()
        {
            BossTurn();
            new AINode_SetReelRespawnDelay { Value = RespawnDelayPhase2 }.Tick(NewContext());
            Assert.AreEqual(RespawnDelayPhase2, _service.RespawnDelayTurns);

            BreakReel(FirstReelGuid());

            BossTurn();
            Assert.AreEqual(3, ReelCoords().Count,
                "Fase 2: la reposición baja a un turno, así que la cuenta arranca cada ronda.");
            Assert.AreEqual(CountdownStart, _service.Countdown);
        }

        [Test]
        public void PhaseTwoDelay_SurvivesTheAuthoredDefaultOfTheSpawnNode()
        {
            BossTurn();
            new AINode_SetReelRespawnDelay { Value = RespawnDelayPhase2 }.Tick(NewContext());

            BossTurn(); // El nodo de la fila vuelve a correr con su RespawnDelayTurns autorado en 2.

            Assert.AreEqual(RespawnDelayPhase2, _service.RespawnDelayTurns,
                "El delay de fase tiene que ganarle al valor autorado del nodo, si no la Fase 2 " +
                "se revierte sola al turno siguiente.");
        }

        // ======================================================================
        // HOLD (Fase 2)
        // ======================================================================

        [Test]
        public void Hold_MakesTheMiddleReelStopCancelling()
        {
            BossTurn();
            var middle = _service.Slots[1].ReelGuid;

            new AINode_LockReel { Side = ReelSide.Middle, LockedHp = 999 }.Tick(NewContext());
            RaiseDamage(middle, 10);

            Assert.IsTrue(_service.IsCounting,
                "El rodillo trabado no cancela: en Fase 2 quedan los dos de la punta.");
        }

        [Test]
        public void Hold_GivesTheMiddleReelAnUnbreakableHpPool()
        {
            BossTurn();
            var middle = _service.Slots[1].ReelGuid;

            new AINode_LockReel { Side = ReelSide.Middle, LockedHp = 999 }.Tick(NewContext());

            Assert.AreEqual(999, _attributes.GetAttribute<Health>(middle).Value);
            Assert.IsTrue(_service.Slots[1].Locked);
        }

        [Test]
        public void Hold_LeavesTheOuterReelsAsValidCancelTargets()
        {
            BossTurn();
            new AINode_LockReel { Side = ReelSide.Middle, LockedHp = 999 }.Tick(NewContext());

            RaiseDamage(_service.Slots[0].ReelGuid, 6);

            Assert.IsFalse(_service.IsCounting, "Los dos de la punta siguen desarmando la bomba.");
        }

        [Test]
        public void LockReel_BeforeTheRowExists_FailsSoTheOnceDoesNotLatch()
        {
            var result = new AINode_LockReel { Side = ReelSide.Middle }.Tick(NewContext());

            Assert.AreEqual(AIResult.Failed, result,
                "Sin fila armada el HOLD no puede aplicarse: tiene que fallar para que el Once " +
                "lo reintente el turno siguiente.");
        }

        // ======================================================================
        // Estado por combate
        // ======================================================================

        [Test]
        public void BindingANewBoss_ResetsTheStateOfThePreviousFight()
        {
            BossTurn();
            Assert.AreEqual(3, _service.Slots.Count);

            _service.BindBoss(Guid.NewGuid());

            Assert.AreEqual(0, _service.Slots.Count,
                "El servicio es Global: una pelea nueva no puede heredar las ranuras de la anterior.");
            Assert.IsFalse(_service.IsCounting);
        }

        [Test]
        public void CombatEnd_ClearsTheCountdown()
        {
            BossTurn();

            EventManager.Trigger(EventName.OnCombatEnd);

            Assert.IsFalse(_service.IsCounting);
            Assert.AreEqual(0, _service.Slots.Count);
        }

        [Test]
        public void CountdownChanges_ArePublishedForTheGiantNumber()
        {
            var seen = new List<JackpotCountdownPayload>();
            Action<JackpotCountdownPayload> listener = p => seen.Add(p);
            TypedEvent<JackpotCountdownPayload>.Subscribe(listener);
            try
            {
                BossTurn(); // Arma en 2.
                BossTurn(); // 1

                Assert.IsNotEmpty(seen,
                    "Si la cuenta no se publica no hay número gigante, y el jackpot pasa a ser un " +
                    "golpe sorpresa de 25.");
                Assert.AreEqual(1, seen[seen.Count - 1].Value);
                Assert.IsTrue(seen[seen.Count - 1].IsCounting);
            }
            finally { TypedEvent<JackpotCountdownPayload>.Unsubscribe(listener); }
        }

        // ======================================================================
        // Harness
        // ======================================================================

        /// <summary>
        /// Mismo orden que el <c>Sequence</c> raíz del builder, sin ExecuteTelegraph (no hay
        /// servicio de amenazas acá) ni gate de fase (los tests de fase disparan sus nodos a mano).
        /// </summary>
        private void BossTurn()
        {
            var ctx = NewContext();
            _tick.Tick(ctx);
            _reels.Tick(ctx);
            _pool.Tick(ctx);
        }

        private void BuildTree()
        {
            _tick = new AINode_TickJackpot();
            _reels = new AINode_SpawnReels
            {
                ReelData = _reelData,
                Count = 3,
                RespawnDelayTurns = RespawnDelayPhase1,
                CountdownOnRespawn = CountdownStart,
                Direction = AINode_SpawnReels.RowDirection.Auto,
            };
            _jackpotProbe = new ProbeNode();
            _pool = new AINode_Selector
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcJackpotCountdown
                            {
                                Comparison = IntComparison.Equal,
                                Value = 0,
                                RequireCounting = true,
                            },
                        },
                        Then = new AINode_Sequence
                        {
                            Children = new List<AIDecisionNode>
                            {
                                _jackpotProbe,
                                new AINode_ResetJackpotCountdown { Value = CountdownStart },
                            },
                        },
                    },
                    new AINode_Wait(),
                },
            };
        }

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = 140,
            Grid = _grid,
            Attributes = _attributes,
            Rng = new System.Random(7),
        };

        private static ModifiableAttributes NewStats(int hp)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            return attrs;
        }

        private List<GridCoord> ReelCoords()
        {
            var coords = new List<GridCoord>();
            foreach (var slot in _service.Slots)
            {
                if (slot.IsAlive) coords.Add(slot.Coord);
            }
            return coords;
        }

        private static HashSet<int> DistinctYs(List<GridCoord> coords)
        {
            var ys = new HashSet<int>();
            foreach (var c in coords) ys.Add(c.Y);
            return ys;
        }

        private Guid FirstReelGuid()
        {
            foreach (var slot in _service.Slots)
            {
                if (slot.IsAlive) return slot.ReelGuid;
            }
            return Guid.Empty;
        }

        private GridCoord CoordOf(Guid reelGuid)
        {
            foreach (var slot in _service.Slots)
            {
                if (slot.ReelGuid == reelGuid) return slot.Coord;
            }
            return default;
        }

        private static void RaiseDamage(Guid target, int finalDamage) =>
            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = target,
                FinalDamage = finalDamage,
                WasLethal = false,
            });

        /// <summary>
        /// Rompe un rodillo como lo haría el jugador: el hook de daño primero (cancela la cuenta) y
        /// después el entierro que hace <c>CombatDeathWatcher</c> — Health en 0 y fuera de la cola.
        /// </summary>
        private void BreakReel(Guid reelGuid)
        {
            RaiseDamage(reelGuid, 6);
            _attributes.SetAttributeValue<Health, int>(reelGuid, 0);
            _turnOrder.Remove(reelGuid);
            _grid.Unregister(reelGuid);
        }

        /// <summary>Hoja de test: cuenta cuántas veces la ejecutó el árbol.</summary>
        private sealed class ProbeNode : AIActionNode
        {
            public int Fired;

            public override AIResult Tick(AIContext context)
            {
                Fired++;
                return AIResult.Succeeded;
            }
        }
    }
}
