using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// El rodillo roto de La Bandida contra el <see cref="HazardService"/> real: su casilla queda
    /// ardiendo. El fuego es el asset del Croupier reusado, así que acá se verifica el cableado y
    /// la casilla exacta, no los números de la sustancia.
    /// </summary>
    [TestFixture]
    public class BandidaReelFireTests
    {
        private const int ReelHp = 60;
        private const int MedianPlayerTurn = 42;
        private const int FireDamage = 6;
        private const int FireDurationRounds = 2;
        private const int RespawnDelayPhase1 = 2;
        private const int CountdownStart = 2;

        private GridManager _grid;
        private AttributesManager _attributes;
        private InMemoryEntityRegistry _registry;
        private TurnOrderService _turnOrder;
        private BandidaJackpotService _jackpot;
        private HazardService _hazard;
        private SpyDamagePipeline _pipeline;

        private EnemyDataSO _reelData;
        private HazardDefinitionSO _fire;

        private Guid _boss;
        private Guid _player;

        private AINode_SpawnReels _reels;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<JackpotCountdownPayload>.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(11, 7));
            ServiceLocator.AddService<IGridManager>(_grid);

            _attributes = new AttributesManager();
            _registry = new InMemoryEntityRegistry();
            _turnOrder = new TurnOrderService();
            ServiceLocator.AddService<InMemoryEntityRegistry>(_registry);
            ServiceLocator.AddService<TurnOrderService>(_turnOrder);

            _pipeline = new SpyDamagePipeline();
            ServiceLocator.AddService<IDamagePipeline>(_pipeline);

            _jackpot = new BandidaJackpotService();
            _jackpot.Register();

            _hazard = new HazardService();
            _hazard.Register();

            _fire = CreateFire();

            _reelData = ScriptableObject.CreateInstance<EnemyDataSO>();
            _reelData.BaseHP = ReelHp;
            _reelData.BaseAttack = 0;
            _reelData.AIRoot = new AINode_Wait();

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

            // Atornillada a la pared de arriba: la fila cae alineada en la línea de abajo (y = 5).
            _grid.Register(_boss, new GridCoord(5, 6));
            _grid.Register(_player, new GridCoord(1, 1));

            // El fuego es PlayerOnly y el filtro es fail-closed: sin IPlayerService no cobra a nadie.
            ServiceLocator.AddService<IPlayerService>(new StubPlayerService { PlayerGuid = _player });
            _attributes.Register(_boss, NewStats(140));
            _turnOrder.RestoreState(new[] { _player, _boss }, cursor: 1, roundIndex: 0);

            _reels = new AINode_SpawnReels
            {
                ReelData = _reelData,
                OnBreakHazard = _fire,
                Count = 3,
                RespawnDelayTurns = RespawnDelayPhase1,
                CountdownOnRespawn = CountdownStart,
                Direction = AINode_SpawnReels.RowDirection.Auto,
            };
        }

        [TearDown]
        public void TearDown()
        {
            _hazard.Dispose();
            _jackpot.Dispose();

            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay is IDisposable d)
                d.Dispose();
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            if (_fire != null) UnityEngine.Object.DestroyImmediate(_fire);
            if (_reelData != null) UnityEngine.Object.DestroyImmediate(_reelData);

            _attributes.Dispose();
            TypedEvent<DamageResolvedPayload>.Clear();
            TypedEvent<JackpotCountdownPayload>.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        // ======================================================================
        // 60 de vida: el rodillo es una pared, no un vidrio
        // ======================================================================

        [Test]
        public void ReelSpawns_WithTheFullHpPoolOfItsData()
        {
            // Arrange / Act
            BossTurn();

            // Assert
            Assert.AreEqual(ReelHp, _attributes.GetAttribute<Health>(FirstReelGuid()).Value,
                "A 3 de vida cualquier golpe partía cualquier rodillo y la elección no existía.");
        }

        [Test]
        public void ReelHitByAMedianTurn_SurvivesAndKeepsItsSlot()
        {
            // Arrange
            BossTurn();
            var reel = FirstReelGuid();

            // Act — el turno mediano del jugador, entero contra un solo rodillo.
            Damage(reel, MedianPlayerTurn);
            BossTurn();

            // Assert
            Assert.AreEqual(3, AliveReelCount(), "Un turno mediano no alcanza para romper un rodillo.");
            Assert.AreEqual(ReelHp - MedianPlayerTurn, _attributes.GetAttribute<Health>(reel).Value);
            Assert.IsEmpty(Instances(), "Un rodillo dañado pero vivo no prende nada: sólo el roto arde.");
        }

        // ======================================================================
        // El rodillo roto deja fuego
        // ======================================================================

        [Test]
        public void BrokenReel_LeavesFireOnItsExactTile()
        {
            // Arrange
            BossTurn();
            var reel = FirstReelGuid();
            var slotCoord = CoordOf(reel);

            // Act
            BreakReel(reel);
            BossTurn();

            // Assert
            var instances = Instances();
            Assert.AreEqual(1, instances.Count, "Un fuego, y sólo por el rodillo que se rompió.");
            Assert.AreSame(_fire, instances[0].Definition,
                "El fuego del rodillo es el del Croupier reusado, no una sustancia nueva.");
            CollectionAssert.AreEquivalent(new[] { slotCoord }, new List<GridCoord>(instances[0].Tiles),
                "Arde la casilla del rodillo: justo la que hace falta para llegar al siguiente.");
            Assert.AreEqual(FireDurationRounds, instances[0].RemainingRounds);
        }

        [Test]
        public void ReelFire_BillsSixOnTheFirstTurnEndInsideIt()
        {
            // Arrange
            BossTurn();
            var reel = FirstReelGuid();
            var slotCoord = CoordOf(reel);
            BreakReel(reel);
            BossTurn();

            // Act — el jugador ocupa el hueco que abrió y cierra el turno ahí.
            _grid.Move(_player, slotCoord);
            EndTurn(_player);

            // Assert
            Assert.AreEqual(1, _pipeline.Resolved.Count);
            Assert.AreEqual(FireDamage, _pipeline.Resolved[0].BaseDamage);
            Assert.AreEqual(_player, _pipeline.Resolved[0].TargetId);
            Assert.AreEqual(AttackKind.Environmental, _pipeline.Resolved[0].Kind);
        }

        [Test]
        public void ReelFire_DoesNotBillTheTurnOfTheBreak()
        {
            // Arrange — rompe el rodillo y se para en el hueco en el MISMO turno.
            BossTurn();
            var reel = FirstReelGuid();
            var slotCoord = CoordOf(reel);
            BreakReel(reel);

            // Act
            _grid.Move(_player, slotCoord);
            EndTurn(_player);

            // Assert
            Assert.IsEmpty(_pipeline.Resolved,
                "La rotura se descubre en el turno del jefe: el turno en que rompés es gratis y el " +
                "precio empieza a correr desde el siguiente.");
        }

        [Test]
        public void TwoBrokenReels_LeaveTwoIndependentFires()
        {
            // Arrange
            BossTurn();
            var first = _jackpot.Slots[0].ReelGuid;
            var second = _jackpot.Slots[2].ReelGuid;
            var coords = new[] { CoordOf(first), CoordOf(second) };

            // Act
            BreakReel(first);
            BreakReel(second);
            BossTurn();

            // Assert
            var instances = Instances();
            Assert.AreEqual(2, instances.Count,
                "Cada rotura es su propia llama: una sola instancia compartida haría que la segunda " +
                "rotura le reiniciara la duración a la primera.");
            foreach (var coord in coords)
                Assert.IsTrue(_hazard.TryGetHazardAt(coord, out _), $"{coord} debería estar ardiendo.");
        }

        [Test]
        public void BrokenReel_IgnitesOnce_NotEveryTurnTheSlotStaysEmpty()
        {
            // Arrange — reposición larga: la ranura queda vacía varios turnos seguidos.
            BossTurn();
            _jackpot.SetRespawnDelay(5);
            BreakReel(FirstReelGuid());

            // Act
            BossTurn();
            BossTurn();
            BossTurn();

            // Assert
            Assert.AreEqual(2, AliveReelCount(), "La ranura sigue vacía: el escenario del test.");
            Assert.AreEqual(1, Instances().Count,
                "La ranura vacía no puede volver a prender cada turno: el fuego lo enciende la " +
                "rotura, no el hueco.");
        }

        [Test]
        public void RespawnedReel_ComesBackOnTopOfItsOwnFire()
        {
            // Arrange
            BossTurn();
            var reel = FirstReelGuid();
            var slotCoord = CoordOf(reel);
            BreakReel(reel);

            // Act
            BossTurn(); // Se descubre la rotura y prende.
            BossTurn(); // Fase 1: el rodillo vuelve a los dos turnos.

            // Assert
            Assert.AreEqual(3, AliveReelCount(), "El fuego no ocupa la casilla: no puede trabar la reposición.");
            CollectionAssert.Contains(ReelCoords(), slotCoord);
            Assert.IsTrue(_hazard.TryGetHazardAt(slotCoord, out _),
                "La llama sigue viva bajo el rodillo repuesto — su reloj corre por rondas, no por " +
                "quién esté parado encima.");
        }

        [Test]
        public void ReelRow_WithoutAHazard_StillDetachesAndRespawns()
        {
            // Arrange — la ficha del jefe sin fuego autorado.
            _reels.OnBreakHazard = null;
            BossTurn();
            var reel = FirstReelGuid();

            // Act
            BreakReel(reel);
            BossTurn();
            BossTurn();

            // Assert
            Assert.IsEmpty(Instances(), "Sin definición no hay fuego, y tampoco excepción.");
            Assert.AreEqual(3, AliveReelCount(), "El ciclo de reposición no depende del fuego.");
        }

        // ======================================================================
        // Lo que el fuego no puede romper
        // ======================================================================

        [Test]
        public void DamagingAReel_StillCancelsTheJackpot_WithTheFireWired()
        {
            // Arrange
            BossTurn();
            Assert.IsTrue(_jackpot.IsCounting);

            // Act — un golpe que NO rompe.
            Damage(FirstReelGuid(), MedianPlayerTurn);

            // Assert
            Assert.IsFalse(_jackpot.IsCounting,
                "La cancelación va por el hook de daño del rodillo: subir su vida a 60 no puede " +
                "haber movido eso a un chequeo de rotura.");
            Assert.IsEmpty(Instances(), "Cancelar no prende nada — el fuego lo deja la rotura.");
        }

        [Test]
        public void BreakingAReel_CancelsTheCountAndLeavesFire_InTheSamePlay()
        {
            // Arrange
            BossTurn();
            var reel = FirstReelGuid();

            // Act
            BreakReel(reel);
            BossTurn();

            // Assert
            Assert.IsFalse(_jackpot.IsCounting, "Romper el rodillo cancela la cuenta.");
            Assert.AreEqual(1, Instances().Count, "…y deja la casilla ardiendo.");
        }

        // ======================================================================
        // Harness
        // ======================================================================

        /// <summary>El turno del jefe reducido a la fila de rodillos.</summary>
        private void BossTurn() => _reels.Tick(NewContext());

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            SelfMaxHp = 140,
            Grid = _grid,
            Attributes = _attributes,
            DamagePipeline = _pipeline,
            Rng = new System.Random(7),
        };

        private static ModifiableAttributes NewStats(int hp)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            return attrs;
        }

        private static HazardDefinitionSO CreateFire()
        {
            // Copia en memoria de HZ_Croupier_TableFire: mismos campos que autora CroupierAssetBuilder.
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.name = "Table Fire";
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Trigger = HazardTriggerMode.OnTurnEndInTile;
            def.Damage = FireDamage;
            def.Kind = AttackKind.Environmental;
            def.DurationRounds = FireDurationRounds;
            def.ConsumeOnTrigger = false;
            def.SourceId = Guid.NewGuid().ToString();
            return def;
        }

        private List<HazardInstanceInfo> Instances() => new List<HazardInstanceInfo>(_hazard.ActiveInstances());

        private List<GridCoord> ReelCoords()
        {
            var coords = new List<GridCoord>();
            foreach (var slot in _jackpot.Slots)
            {
                if (slot.IsAlive) coords.Add(slot.Coord);
            }
            return coords;
        }

        private int AliveReelCount() => ReelCoords().Count;

        private Guid FirstReelGuid()
        {
            foreach (var slot in _jackpot.Slots)
            {
                if (slot.IsAlive) return slot.ReelGuid;
            }
            return Guid.Empty;
        }

        private GridCoord CoordOf(Guid reelGuid)
        {
            foreach (var slot in _jackpot.Slots)
            {
                if (slot.ReelGuid == reelGuid) return slot.Coord;
            }
            return default;
        }

        /// <summary>Baja vida y dispara el hook de daño, como un golpe real del jugador.</summary>
        private void Damage(Guid reelGuid, int amount)
        {
            var health = _attributes.GetAttribute<Health>(reelGuid);
            int remaining = Mathf.Max(0, health.Value - amount);
            _attributes.SetAttributeValue<Health, int>(reelGuid, remaining);

            TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
            {
                SourceGuid = _player,
                TargetGuid = reelGuid,
                FinalDamage = amount,
                WasLethal = remaining <= 0,
            });
        }

        /// <summary>
        /// Golpe letal más el entierro que hace <c>CombatDeathWatcher</c>: fuera de la cola y fuera
        /// del grid, con la casilla liberada.
        /// </summary>
        private void BreakReel(Guid reelGuid)
        {
            Damage(reelGuid, ReelHp);
            _turnOrder.Remove(reelGuid);
            _grid.Unregister(reelGuid);
        }

        private static void EndTurn(Guid entity) => EventManager.Trigger(EventName.OnTurnFinished, entity);

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

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; } = Guid.NewGuid();
            public Guid RunId { get; set; } = Guid.NewGuid();
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag { get; set; }
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { DiceBag = bag; }
            public void ClearPlayer() { }
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
        }
    }
}
