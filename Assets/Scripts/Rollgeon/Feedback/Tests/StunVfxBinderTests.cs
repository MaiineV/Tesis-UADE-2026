using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using UnityEngine;

namespace Rollgeon.Feedback.Tests
{
    /// <summary>
    /// <see cref="StunVfxBinder"/> (BUG-87): spawn de partículas sobre el pawn al
    /// aplicarse el stun, destroy al expirar / morir la entidad, y cleanup total de
    /// scope (StunService.ClearAll no emite OnStunExpired por entidad).
    /// </summary>
    [TestFixture]
    public class StunVfxBinderTests
    {
        private StunVfxBinder _binder;
        private FakePawnRegistry _registry;
        private GameObject _pawn;
        private GameObject _fakePrefab;
        private Guid _guid;

        private sealed class FakePawnRegistry : IPawnRegistry
        {
            public readonly Dictionary<Guid, Transform> Pawns = new();
            public void Register(Guid entityGuid, Transform pawn) => Pawns[entityGuid] = pawn;
            public void Unregister(Guid entityGuid) => Pawns.Remove(entityGuid);
            public bool TryGetTransform(Guid entityGuid, out Transform pawn)
                => Pawns.TryGetValue(entityGuid, out pawn) && pawn != null;
        }

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.RemoveService<StunVfxBinder>();
            _binder = new StunVfxBinder();
            _binder.Register();

            _registry = new FakePawnRegistry();
            ServiceLocator.AddService<IPawnRegistry>(_registry, ServiceScope.Run);

            _guid = Guid.NewGuid();
            _pawn = new GameObject("pawn");
            _registry.Register(_guid, _pawn.transform);

            // Prefab inyectado por reflection: Resources/VFX_StunStars puede no
            // existir todavía (lo crea el builder) y el test no depende del asset.
            _fakePrefab = new GameObject("VFX_StunStars_Fake");
            typeof(StunVfxBinder)
                .GetField("_prefab", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_binder, _fakePrefab);
        }

        [TearDown]
        public void TearDown()
        {
            _binder.Dispose();
            ServiceLocator.RemoveService<StunVfxBinder>();
            ServiceLocator.RemoveService<IPawnRegistry>();
            EventManager.ResetEventDictionary();
            if (_pawn != null) UnityEngine.Object.DestroyImmediate(_pawn);
            if (_fakePrefab != null) UnityEngine.Object.DestroyImmediate(_fakePrefab);
        }

        [Test]
        public void should_spawn_vfx_as_pawn_child_when_stun_applied()
        {
            // Arrange (SetUp) + Act
            EventManager.Trigger(EventName.OnStunApplied, _guid, 1);

            // Assert
            Assert.AreEqual(1, _pawn.transform.childCount,
                "El VFX debe instanciarse como hijo del pawn.");
        }

        [Test]
        public void should_not_duplicate_vfx_when_stun_reapplied()
        {
            // Arrange
            EventManager.Trigger(EventName.OnStunApplied, _guid, 1);

            // Act — refresco del stun (max(actual, nuevo)) — mismo VFX.
            EventManager.Trigger(EventName.OnStunApplied, _guid, 2);

            // Assert
            Assert.AreEqual(1, _pawn.transform.childCount);
        }

        [Test]
        public void should_destroy_vfx_when_stun_expired()
        {
            // Arrange
            EventManager.Trigger(EventName.OnStunApplied, _guid, 1);

            // Act
            EventManager.Trigger(EventName.OnStunExpired, _guid);

            // Assert
            Assert.AreEqual(0, _pawn.transform.childCount,
                "Al expirar el stun el VFX debe destruirse.");
        }

        [Test]
        public void should_clear_all_vfx_when_combat_ends()
        {
            // Arrange — StunService.ClearAll no emite OnStunExpired: el scope-end
            // del binder es la única limpieza.
            EventManager.Trigger(EventName.OnStunApplied, _guid, 1);

            // Act
            EventManager.Trigger(EventName.OnCombatEnd, Guid.NewGuid());

            // Assert
            Assert.AreEqual(0, _pawn.transform.childCount);
        }

        [Test]
        public void should_noop_without_registered_pawn()
        {
            // Arrange
            var unknown = Guid.NewGuid();

            // Act + Assert — sin pawn no hay dónde spawnear; no debe loguear error.
            Assert.DoesNotThrow(() => EventManager.Trigger(EventName.OnStunApplied, unknown, 1));
        }
    }
}
