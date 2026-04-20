using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Player;
using Rollgeon.Heroes;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="BasicEnemyAI"/> (S#0012b).
    /// </summary>
    [TestFixture]
    public class BasicEnemyAITests
    {
        private AttributesManager _attrManager;
        private FakePlayerService _playerService;
        private Guid _playerId;
        private Guid _enemyId;
        private int _turnCompleteCount;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();

            _attrManager = new AttributesManager();
            _playerId = Guid.NewGuid();
            _enemyId = Guid.NewGuid();
            _turnCompleteCount = 0;

            _playerService = new FakePlayerService(_playerId);

            // Register player with 100 HP
            var playerAttrs = new ModifiableAttributes();
            playerAttrs.EnsureInitialized();
            playerAttrs.SetAttribute<Health>(new Health(100));
            _attrManager.Register(_playerId, playerAttrs);

            AttributesManager.LogMissingEntityAsWarning = true;
        }

        [TearDown]
        public void TearDown()
        {
            _attrManager.Dispose();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private void RegisterEnemy(Guid id, int attack, int hp = 50)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            attrs.SetAttribute<Attack>(new Attack(attack));
            _attrManager.Register(id, attrs);
        }

        private BasicEnemyAI CreateAI(IDamagePipeline pipeline = null)
        {
            pipeline ??= new DamagePipeline(_attrManager);
            return new BasicEnemyAI(
                _attrManager,
                _playerService,
                pipeline,
                () => _turnCompleteCount++);
        }

        // ── 1. Positive attack deals damage ─────────────────────────────

        [Test]
        public void HandleEnemyTurn_WithPositiveAttack_DealsDamageToPlayer()
        {
            RegisterEnemy(_enemyId, attack: 10);
            var ai = CreateAI();

            ai.HandleEnemyTurn(_enemyId);

            int hp = _attrManager.GetAttribute<Health>(_playerId).Value;
            Assert.AreEqual(90, hp, "Player HP should be reduced by enemy attack value.");
        }

        // ── 2. Zero attack skips and completes turn ─────────────────────

        [Test]
        public void HandleEnemyTurn_WithZeroAttack_SkipsAttackAndCompletesTurn()
        {
            RegisterEnemy(_enemyId, attack: 0);
            var ai = CreateAI();

            ai.HandleEnemyTurn(_enemyId);

            int hp = _attrManager.GetAttribute<Health>(_playerId).Value;
            Assert.AreEqual(100, hp, "Player HP should be unchanged for zero-attack enemy.");
            Assert.AreEqual(1, _turnCompleteCount, "Turn complete should still be invoked.");
        }

        // ── 3. Always signals turn complete ─────────────────────────────

        [Test]
        public void HandleEnemyTurn_AlwaysSignalsTurnComplete()
        {
            RegisterEnemy(_enemyId, attack: 15);
            var ai = CreateAI();

            ai.HandleEnemyTurn(_enemyId);

            Assert.AreEqual(1, _turnCompleteCount, "onTurnComplete should be invoked exactly once.");
        }

        // ── 4. Uses damage pipeline ─────────────────────────────────────

        [Test]
        public void HandleEnemyTurn_UsesDamagePipeline()
        {
            RegisterEnemy(_enemyId, attack: 25);
            var spy = new SpyDamagePipeline();
            var ai = CreateAI(spy);

            ai.HandleEnemyTurn(_enemyId);

            Assert.IsTrue(spy.ResolveCalled, "DamagePipeline.Resolve should have been called.");
            Assert.AreEqual(_enemyId, spy.LastContext.SourceId);
            Assert.AreEqual(_playerId, spy.LastContext.TargetId);
            Assert.AreEqual(25, spy.LastContext.BaseDamage);
            Assert.AreEqual(AttackKind.BasicAttack, spy.LastContext.Kind);
        }

        // ── 5. Unregistered enemy logs warning and completes turn ───────

        [Test]
        public void HandleEnemyTurn_WithUnregisteredEnemy_LogsWarningAndCompletesTurn()
        {
            var unregisteredId = Guid.NewGuid();
            var ai = CreateAI();

            // Should not throw — graceful degradation
            Assert.DoesNotThrow(() => ai.HandleEnemyTurn(unregisteredId));
            Assert.AreEqual(1, _turnCompleteCount,
                "Turn complete should be invoked even for unregistered enemies.");
        }

        // ── 6. Lethal damage sets context lethal ────────────────────────

        [Test]
        public void HandleEnemyTurn_LethalDamage_SetsContextLethal()
        {
            RegisterEnemy(_enemyId, attack: 150);
            DamageResolvedPayload? captured = null;
            TypedEvent<DamageResolvedPayload>.Subscribe(p => captured = p);

            var ai = CreateAI();
            ai.HandleEnemyTurn(_enemyId);

            int hp = _attrManager.GetAttribute<Health>(_playerId).Value;
            Assert.AreEqual(0, hp, "Player HP should be clamped to 0.");
            Assert.IsTrue(captured.HasValue, "DamageResolvedPayload should have been raised.");
            Assert.AreEqual(150, captured.Value.FinalDamage);
        }

        // ── 7. Null dependencies throw ArgumentNullException ────────────

        [Test]
        public void Constructor_NullAttributes_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BasicEnemyAI(null, _playerService, new DamagePipeline(_attrManager), () => { }));
        }

        [Test]
        public void Constructor_NullPlayerService_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BasicEnemyAI(_attrManager, null, new DamagePipeline(_attrManager), () => { }));
        }

        [Test]
        public void Constructor_NullDamagePipeline_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BasicEnemyAI(_attrManager, _playerService, null, () => { }));
        }

        [Test]
        public void Constructor_NullCallback_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new BasicEnemyAI(_attrManager, _playerService, new DamagePipeline(_attrManager), null));
        }

        // ── Fakes ───────────────────────────────────────────────────────

        private class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; }
            public Guid RunId => Guid.NewGuid();
            public ClassHeroSO CurrentHero => null;

            public FakePlayerService(Guid playerId) { PlayerGuid = playerId; }

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void ClearPlayer() { }

#pragma warning disable CS0067
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore CS0067
        }

        private class SpyDamagePipeline : IDamagePipeline
        {
            public bool ResolveCalled;
            public DamageContext LastContext;

            public DamageContext Resolve(DamageContext ctx)
            {
                ResolveCalled = true;
                LastContext = ctx;
                ctx.FinalDamage = ctx.BaseDamage;
                ctx.WasLethal = false;
                return ctx;
            }
        }
    }
}
