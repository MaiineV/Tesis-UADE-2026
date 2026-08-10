using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="HazardService"/>: el runner genérico que reemplazó el loop hardcoded de
    /// <see cref="RainHazardService"/>. Cualquier cantidad de <see cref="HazardDefinitionSO"/>
    /// puede estar activa a la vez, cada una con su propia cadencia y su propio source id — este
    /// suite cubre esa coexistencia (ver <see cref="RainHazardServiceTests"/> para el shim de rain
    /// en sí, que debe seguir comportándose idéntico).
    /// </summary>
    [TestFixture]
    public class HazardServiceTests
    {
        private GridManager _grid;
        private TurnOrderService _turnOrder;
        private ThreatenedAreaService _threat;
        private StubPlayerService _playerService;
        private HazardService _hazard;
        private Guid _playerGuid;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid);

            _threat = new ThreatenedAreaService();
            _threat.Register();

            _playerGuid = Guid.NewGuid();
            _grid.Register(_playerGuid, new GridCoord(4, 4)); // SquareAroundPlayer necesita una posición real.
            _playerService = new StubPlayerService { PlayerGuid = _playerGuid };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            ServiceLocator.AddService<IDamagePipeline>(new SpyDamagePipeline());

            _turnOrder = new TurnOrderService();

            _hazard = new HazardService();
            _hazard.Register();
        }

        [TearDown]
        public void TearDown()
        {
            // AINode_TelegraphMark dispara ThreatTelegraphOverlay.ResolveOrCreate() al marcar,
            // que crea un GameObject "ThreatTelegraphOverlay" en la escena — limpiarlo, si no
            // queda huérfano y contamina tests posteriores que lo buscan por nombre.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay)
                && overlay is IDisposable disposable)
            {
                disposable.Dispose();
            }
            var leftover = GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void FireRound(int roundIndex)
        {
            _turnOrder.RestoreState(new[] { _playerGuid }, cursor: 0, roundIndex: roundIndex);
        }

        private static HazardDefinitionSO CreateDefinition(
            ThreatShape shape, int size, int count, int cycleRounds, int damage, AttackKind kind, Guid sourceId)
        {
            var def = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.Shape = shape;
            def.Size = size;
            def.Count = count;
            def.CycleRounds = cycleRounds;
            def.Damage = damage;
            def.Kind = kind;
            def.SourceId = sourceId.ToString();
            return def;
        }

        [Test]
        public void Activate_TwoHazardsWithDifferentCadences_EachTelegraphsOnItsOwnCycle()
        {
            // Arrange
            var rain = CreateDefinition(ThreatShape.ScatteredSquares, size: 1, count: 2, cycleRounds: 2,
                damage: 6, kind: AttackKind.Environmental, sourceId: Guid.NewGuid());
            var fire = CreateDefinition(ThreatShape.SquareAroundPlayer, size: 1, count: 1, cycleRounds: 3,
                damage: 8, kind: AttackKind.DamageOverTime, sourceId: Guid.NewGuid());
            _hazard.Activate(rain);
            _hazard.Activate(fire);

            // Act — ronda 2: solo la cadencia de rain (cada 2) cae.
            FireRound(2);

            // Assert
            Assert.IsTrue(_threat.HasPending(rain.SourceGuid), "Rain debería marcar en su cadencia (cada 2 rondas).");
            Assert.IsFalse(_threat.HasPending(fire.SourceGuid), "Fire no debería marcar todavía (cadencia cada 3 rondas).");

            // Act — ronda 3: cae la cadencia de fire; la de rain no vuelve a caer (3 % 2 != 0).
            FireRound(3);

            // Assert
            Assert.IsTrue(_threat.HasPending(fire.SourceGuid), "Fire debería marcar en la ronda 3 (su cadencia).");
            Assert.IsTrue(_threat.HasPending(rain.SourceGuid), "La marca de rain no debería tocarse en una ronda que no es múltiplo de su cadencia.");
        }

        [Test]
        public void Reset_ClearsAllActiveHazards_RegardlessOfCount()
        {
            // Arrange
            var rain = CreateDefinition(ThreatShape.ScatteredSquares, size: 1, count: 2, cycleRounds: 2,
                damage: 6, kind: AttackKind.Environmental, sourceId: Guid.NewGuid());
            var fire = CreateDefinition(ThreatShape.SquareAroundPlayer, size: 1, count: 1, cycleRounds: 2,
                damage: 8, kind: AttackKind.DamageOverTime, sourceId: Guid.NewGuid());
            _hazard.Activate(rain);
            _hazard.Activate(fire);
            FireRound(2);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());

            // Assert
            Assert.IsFalse(_hazard.IsActive(rain), "OnCombatEnd debería desactivar todos los hazards activos, no solo uno.");
            Assert.IsFalse(_hazard.IsActive(fire), "OnCombatEnd debería desactivar todos los hazards activos, no solo uno.");
            Assert.IsFalse(_threat.HasPending(rain.SourceGuid));
            Assert.IsFalse(_threat.HasPending(fire.SourceGuid));
        }

        [Test]
        public void Activate_SameDefinitionTwice_IsIdempotent()
        {
            // Arrange
            var fire = CreateDefinition(ThreatShape.SquareAroundPlayer, size: 1, count: 1, cycleRounds: 2,
                damage: 8, kind: AttackKind.DamageOverTime, sourceId: Guid.NewGuid());

            // Act
            _hazard.Activate(fire);
            _hazard.Activate(fire);

            // Assert
            Assert.IsTrue(_hazard.IsActive(fire));
        }

        [Test]
        public void IsActive_UnknownSourceId_ReturnsFalse()
        {
            // Arrange
            var unknownId = Guid.NewGuid();

            // Act
            var result = _hazard.IsActive(unknownId);

            // Assert
            Assert.IsFalse(result);
        }

        private class StubPlayerService : IPlayerService
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

        private class SpyDamagePipeline : IDamagePipeline
        {
            public DamageContext Resolve(DamageContext ctx) { ctx.FinalDamage = ctx.BaseDamage; return ctx; }
            public DamageContext Preview(DamageContext ctx) { ctx.FinalDamage = ctx.BaseDamage; return ctx; }
        }
    }
}
