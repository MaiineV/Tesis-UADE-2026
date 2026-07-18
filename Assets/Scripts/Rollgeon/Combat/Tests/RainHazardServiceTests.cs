using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Dice;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Player;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// Tests de <see cref="RainHazardService"/>: amenaza ambiental independiente del boss —
    /// inactiva hasta <see cref="RainHazardService.Activate"/>, después corre en su propio
    /// ciclo de rondas sin pisar el pending del boss.
    /// </summary>
    [TestFixture]
    public class RainHazardServiceTests
    {
        private GridManager _grid;
        private TurnOrderService _turnOrder;
        private ThreatenedAreaService _threat;
        private StubPlayerService _playerService;
        private RainHazardService _rain;
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
            _playerService = new StubPlayerService { PlayerGuid = _playerGuid };
            ServiceLocator.AddService<IPlayerService>(_playerService);

            ServiceLocator.AddService<IDamagePipeline>(new SpyDamagePipeline());

            _turnOrder = new TurnOrderService();

            _rain = new RainHazardService();
            _rain.Register();
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
            var leftover = UnityEngine.GameObject.Find("ThreatTelegraphOverlay");
            if (leftover != null) UnityEngine.Object.DestroyImmediate(leftover);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private void FireRound(int roundIndex)
        {
            _turnOrder.RestoreState(new[] { _playerGuid }, cursor: 0, roundIndex: roundIndex);
        }

        [Test]
        public void Inactive_NeverMarksAnything_RegardlessOfRounds()
        {
            FireRound(2);
            FireRound(4);

            Assert.IsFalse(_threat.HasPending(RainHazardService.RainSourceId));
        }

        [Test]
        public void Activate_ThenRoundMultiple_MarksTiles()
        {
            _rain.Activate();

            FireRound(2);

            Assert.IsTrue(_threat.HasPending(RainHazardService.RainSourceId));
        }

        [Test]
        public void Activate_NonMultipleRound_DoesNotMarkYet()
        {
            _rain.Activate();

            FireRound(1);

            Assert.IsFalse(_threat.HasPending(RainHazardService.RainSourceId));
        }

        [Test]
        public void Activate_IsIdempotent()
        {
            _rain.Activate();
            _rain.Activate();

            Assert.IsTrue(_rain.IsActive);
        }

        [Test]
        public void SecondCycle_DetonatesFirstMark_AndMarksAgain()
        {
            _rain.Activate();

            FireRound(2);
            Assert.IsTrue(_threat.HasPending(RainHazardService.RainSourceId));

            FireRound(4);

            // Detona lo marcado en la ronda 2 y vuelve a marcar en el mismo tick — sigue pendiente.
            Assert.IsTrue(_threat.HasPending(RainHazardService.RainSourceId));
        }

        [Test]
        public void DoesNotInterfereWithBossOwnPendingArea()
        {
            var bossGuid = Guid.NewGuid();
            _threat.Mark(bossGuid, new[] { new GridCoord(0, 0) }, damage: 5, AttackKind.BasicAttack);

            _rain.Activate();
            FireRound(2);

            Assert.IsTrue(_threat.HasPending(bossGuid), "La marca del boss no debería verse afectada por la lluvia.");
            Assert.IsTrue(_threat.HasPending(RainHazardService.RainSourceId));
        }

        [Test]
        public void CombatEnd_ResetsActiveStateAndClearsPending()
        {
            _rain.Activate();
            FireRound(2);

            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());

            Assert.IsFalse(_rain.IsActive);
            Assert.IsFalse(_threat.HasPending(RainHazardService.RainSourceId));
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
