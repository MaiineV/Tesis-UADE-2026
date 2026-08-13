using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Behaviors;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.Feedback.Tests
{
    /// <summary>
    /// Cobertura del dedup entre ruta A (<c>DamagePipeline</c> →
    /// <c>TypedEvent&lt;DamageResolvedPayload&gt;</c> → <c>FloatingDamageSpawner</c>) y ruta B
    /// (<c>EffDealDamage</c> → <c>FeedbackDB</c> → <see cref="FeedbackManager"/> → evento legacy
    /// <c>OnFloatingNumberRequested</c>). Sin este dedup, todo daño se pintaba dos veces.
    /// Plan feedback-numerico-combate §1/§6.
    /// </summary>
    [TestFixture]
    public class FeedbackManagerFloatingDedupTests
    {
        private sealed class FakeDamagePipeline : IDamagePipeline
        {
            public DamageContext Resolve(DamageContext ctx) => ctx;
            public DamageContext Preview(DamageContext ctx) => ctx;
        }

        private GameObject _go;
        private FeedbackManager _manager;
        private MethodInfo _spawnMethod;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("FeedbackManager");
            _manager = _go.AddComponent<FeedbackManager>();

            // SpawnFloatingNumberDelayed es privado — el manager no expone un hook público
            // para disparar la ruta B sin pasar por RequestFeedbackBlocking + FeedbackDBSO
            // (setup pesado para este test). Reflection es el mismo patrón que ya usa
            // FloatingDamageSpawnerTests.AssignPrivate para inyectar campos privados.
            _spawnMethod = typeof(FeedbackManager).GetMethod(
                "SpawnFloatingNumberDelayed", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(_spawnMethod, "SpawnFloatingNumberDelayed no encontrado — revisar rename.");
        }

        [TearDown]
        public void Teardown()
        {
            ServiceLocator.RemoveService<IDamagePipeline>();
            EventManager.ResetEventDictionary();
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        // ── Helper puro (usado dentro del coroutine) ────────────────────

        [Test]
        public void ShouldSuppress_DamageWithPipeline_ReturnsTrue()
        {
            Assert.IsTrue(FeedbackManager.ShouldSuppressFloatingNumber(
                FloatingNumberType.Damage, pipelinePresent: true));
        }

        [Test]
        public void ShouldSuppress_DamageWithoutPipeline_ReturnsFalse()
        {
            Assert.IsFalse(FeedbackManager.ShouldSuppressFloatingNumber(
                FloatingNumberType.Damage, pipelinePresent: false));
        }

        [Test]
        public void ShouldSuppress_HealNeverSuppressed()
        {
            Assert.IsFalse(FeedbackManager.ShouldSuppressFloatingNumber(FloatingNumberType.Heal, true));
            Assert.IsFalse(FeedbackManager.ShouldSuppressFloatingNumber(FloatingNumberType.Heal, false));
        }

        [Test]
        public void ShouldSuppress_ShieldNeverSuppressed()
        {
            Assert.IsFalse(FeedbackManager.ShouldSuppressFloatingNumber(FloatingNumberType.Shield, true));
            Assert.IsFalse(FeedbackManager.ShouldSuppressFloatingNumber(FloatingNumberType.Shield, false));
        }

        [Test]
        public void ShouldSuppress_StatusNeverSuppressed()
        {
            Assert.IsFalse(FeedbackManager.ShouldSuppressFloatingNumber(FloatingNumberType.Status, true));
        }

        [Test]
        public void ShouldSuppress_GoldNeverSuppressed()
        {
            // El oro no pasa por SpawnFloatingNumberDelayed (lo emite EnemyGoldDropService
            // directo), pero la regla del helper debe seguir siendo Damage-only.
            Assert.IsFalse(FeedbackManager.ShouldSuppressFloatingNumber(FloatingNumberType.Gold, true));
        }

        // ── Integración: la corrutina real respeta el dedup ─────────────
        // Con Delay=0 en el FloatingNumberBehaviorValue no hay ningún yield antes del punto
        // de decisión — el primer (y único) MoveNext() ejecuta todo el cuerpo síncronamente,
        // sin necesitar CoroutineHost ni un frame del Editor.

        [Test]
        public void Coroutine_FloatingDamage_WithPipeline_DoesNotReemit()
        {
            ServiceLocator.AddService<IDamagePipeline>(new FakeDamagePipeline());
            bool fired = false;
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, _ => fired = true);

            RunSpawnCoroutine(BehaviorValueKey.FloatingDamage);

            Assert.IsFalse(fired, "Con IDamagePipeline registrado, ruta B no debe re-emitir daño " +
                                   "— ruta A (TypedEvent<DamageResolvedPayload>) ya lo pintó.");
        }

        [Test]
        public void Coroutine_FloatingDamage_WithoutPipeline_Reemits()
        {
            bool fired = false;
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, _ => fired = true);

            RunSpawnCoroutine(BehaviorValueKey.FloatingDamage);

            Assert.IsTrue(fired, "Sin IDamagePipeline registrado (EditMode / escenas sin bootstrap), " +
                                  "ruta B es el único fallback de display.");
        }

        [Test]
        public void Coroutine_FloatingHeal_AlwaysReemits_EvenWithPipeline()
        {
            ServiceLocator.AddService<IDamagePipeline>(new FakeDamagePipeline());
            bool fired = false;
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, _ => fired = true);

            RunSpawnCoroutine(BehaviorValueKey.FloatingHeal);

            Assert.IsTrue(fired, "Heal no tiene ruta A — siempre debe re-emitir.");
        }

        [Test]
        public void Coroutine_FloatingShield_AlwaysReemits_EvenWithPipeline()
        {
            ServiceLocator.AddService<IDamagePipeline>(new FakeDamagePipeline());
            bool fired = false;
            EventManager.Subscribe(EventName.OnFloatingNumberRequested, _ => fired = true);

            RunSpawnCoroutine(BehaviorValueKey.FloatingShield);

            Assert.IsTrue(fired, "Shield no tiene ruta A — siempre debe re-emitir.");
        }

        private void RunSpawnCoroutine(BehaviorValueKey key)
        {
            var fn = new FloatingNumberBehaviorValue { Value = 5f, Delay = 0f };
            var request = new FeedbackRequest { TargetGuid = Guid.NewGuid() };

            var coroutine = (IEnumerator)_spawnMethod.Invoke(_manager, new object[] { fn, request, key });
            coroutine.MoveNext();
        }
    }
}
