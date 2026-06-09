using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Entities.Tests
{
    /// <summary>
    /// Tests del <see cref="EntityQueryService"/> (runtime impl de
    /// <see cref="IEntityQueryService"/>). Cubre la matriz de relaciones del modelo
    /// 1-player-vs-N-enemigos, los rosters ally/enemy, y — regresión del bug que dejaba
    /// al healer mudo — que el <see cref="EntityQueryServiceBootstrap"/> efectivamente
    /// registra el servicio en el <see cref="ServiceLocator"/>.
    /// </summary>
    [TestFixture]
    public class EntityQueryServiceTests
    {
        private AttributesManager _attrs;
        private StubPlayerService _player;
        private EntityQueryService _sut;

        private Guid _playerGuid;
        private Guid _enemyA;
        private Guid _enemyB;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _playerGuid = new Guid("00000000-0000-0000-0000-0000000000aa");
            _enemyA = new Guid("00000000-0000-0000-0000-0000000000b1");
            _enemyB = new Guid("00000000-0000-0000-0000-0000000000b2");

            RegisterEntity(_playerGuid, hp: 30);
            RegisterEntity(_enemyA, hp: 10);
            RegisterEntity(_enemyB, hp: 10);

            _player = new StubPlayerService { PlayerGuid = _playerGuid };
            ServiceLocator.AddService<IPlayerService>(_player);

            _sut = new EntityQueryService();
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
        }

        private void RegisterEntity(Guid guid, int hp)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            _attrs.Register(guid, attrs);
        }

        // ----- GetRelationship -------------------------------------------

        [Test]
        public void GetRelationship_EnemyOwnerTargetsPlayer_ReturnsEnemiesAndPlayerBits()
        {
            // Arrange — owner enemigo, target player.
            // Act
            var rel = _sut.GetRelationship(_enemyA, _playerGuid);

            // Assert — el player es enemigo del enemigo (Enemies) y además lleva el bit Player,
            // así un selector de IA configurado con Player O con Enemies lo encuentra.
            Assert.AreEqual(EntityFilterMask.Enemies | EntityFilterMask.Player, rel);
        }

        [Test]
        public void GetRelationship_EnemyOwnerTargetsOtherEnemy_ReturnsAllies()
        {
            var rel = _sut.GetRelationship(_enemyA, _enemyB);

            Assert.AreEqual(EntityFilterMask.Allies, rel);
        }

        [Test]
        public void GetRelationship_PlayerOwnerTargetsEnemy_ReturnsEnemiesOnly()
        {
            var rel = _sut.GetRelationship(_playerGuid, _enemyA);

            Assert.AreEqual(EntityFilterMask.Enemies, rel);
            Assert.IsFalse(rel.HasFlag(EntityFilterMask.Player));
        }

        [Test]
        public void GetRelationship_NoPlayerRegistered_ReturnsNone()
        {
            // Arrange — sin player resoluble.
            _player.PlayerGuid = Guid.Empty;

            // Act
            var rel = _sut.GetRelationship(_enemyA, _enemyB);

            // Assert
            Assert.AreEqual(EntityFilterMask.None, rel);
        }

        // ----- GetAllAlliesOf / GetAllEnemiesOf --------------------------

        [Test]
        public void GetAllAlliesOf_EnemyOwner_ReturnsOtherEnemiesExcludingSelfAndPlayer()
        {
            var allies = _sut.GetAllAlliesOf(_enemyA).Select(e => e.Guid).ToList();

            Assert.AreEqual(1, allies.Count);
            Assert.Contains(_enemyB, allies);
            CollectionAssert.DoesNotContain(allies, _enemyA);   // self excluido
            CollectionAssert.DoesNotContain(allies, _playerGuid); // player es otra facción
        }

        [Test]
        public void GetAllEnemiesOf_EnemyOwner_ReturnsOnlyPlayer()
        {
            var enemies = _sut.GetAllEnemiesOf(_enemyA).Select(e => e.Guid).ToList();

            Assert.AreEqual(1, enemies.Count);
            Assert.Contains(_playerGuid, enemies);
        }

        [Test]
        public void GetAllAlliesOf_PlayerOwner_ReturnsEmpty()
        {
            // En el modelo 1-player-vs-N el player no tiene aliados de combate.
            var allies = _sut.GetAllAlliesOf(_playerGuid).ToList();

            Assert.IsEmpty(allies);
        }

        [Test]
        public void GetAllEnemiesOf_PlayerOwner_ReturnsAllEnemies()
        {
            var enemies = _sut.GetAllEnemiesOf(_playerGuid).Select(e => e.Guid).ToList();

            Assert.AreEqual(2, enemies.Count);
            CollectionAssert.AreEquivalent(new[] { _enemyA, _enemyB }, enemies);
        }

        // ----- Bootstrap registration (regresión del hueco original) -----

        [Test]
        public void Bootstrap_Register_RegistersEntityQueryServiceInLocator()
        {
            // Arrange — locator limpio sin el servicio.
            ServiceLocator.Clear();
            Assert.IsFalse(ServiceLocator.HasService<IEntityQueryService>());
            var bootstrap = ScriptableObject.CreateInstance<EntityQueryServiceBootstrap>();

            try
            {
                // Act
                bootstrap.Register();

                // Assert — el servicio queda resoluble por interfaz.
                Assert.IsTrue(ServiceLocator.TryGetService<IEntityQueryService>(out var svc));
                Assert.IsInstanceOf<EntityQueryService>(svc);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bootstrap);
            }
        }

        // ----- stub ------------------------------------------------------

        private sealed class StubPlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId { get; set; }
            public ClassHeroSO CurrentHero => null;
            public DiceBagSO DiceBag => null;

            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }

#pragma warning disable 67 // eventos del contrato — no se disparan en el stub.
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }
    }
}
