using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Entities.Portraits;
using Rollgeon.Heroes;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Entities.Tests
{
    /// <summary>
    /// Cubre <see cref="EntityPortraitResolver"/>: registro explícito guid→sprite,
    /// resolución lazy del player vía <see cref="IPlayerService"/>, precedencia
    /// dict-sobre-lazy y fallbacks (guid desconocido, sprite null, sin player service).
    /// </summary>
    [TestFixture]
    public class EntityPortraitResolverTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
            _createdObjects.Add(sprite);
            return sprite;
        }

        private ClassHeroSO CreateHero(Sprite portrait)
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.Portrait = portrait;
            _createdObjects.Add(hero);
            return hero;
        }

        /// <summary>Fake mínimo — solo PlayerGuid/CurrentHero importan al resolver.</summary>
        private sealed class FakePlayerService : IPlayerService
        {
            public Guid PlayerGuid { get; set; }
            public Guid RunId => Guid.Empty;
            public ClassHeroSO CurrentHero { get; set; }
            public DiceBagSO DiceBag => null;
            public void SetPlayer(ClassHeroSO hero, Guid runId) { }
            public void SetDiceBag(DiceBagSO bag) { }
            public void ClearPlayer() { }
#pragma warning disable 67
            public event Action<ClassHeroSO> OnPlayerSet;
            public event Action OnPlayerCleared;
#pragma warning restore 67
        }

        // -------------------------------------------------------------------
        // Registro explícito
        // -------------------------------------------------------------------

        [Test]
        public void TryGetPortrait_RegisteredGuid_ReturnsRegisteredSprite()
        {
            // Arrange
            var resolver = new EntityPortraitResolver();
            var guid = Guid.NewGuid();
            var sprite = CreateSprite();
            resolver.Register(guid, sprite);

            // Act
            bool found = resolver.TryGetPortrait(guid, out var result);

            // Assert
            Assert.IsTrue(found);
            Assert.AreSame(sprite, result);
        }

        [Test]
        public void TryGetPortrait_UnknownGuid_ReturnsFalse()
        {
            // Arrange
            var resolver = new EntityPortraitResolver();

            // Act
            bool found = resolver.TryGetPortrait(Guid.NewGuid(), out var result);

            // Assert
            Assert.IsFalse(found);
            Assert.IsNull(result);
        }

        [Test]
        public void Register_NullSprite_TryGetReturnsFalse()
        {
            // Arrange
            var resolver = new EntityPortraitResolver();
            var guid = Guid.NewGuid();

            // Act
            resolver.Register(guid, null);

            // Assert
            Assert.IsFalse(resolver.TryGetPortrait(guid, out _),
                "Registrar sprite null debe ser no-op — el caller usa su fallback.");
        }

        [Test]
        public void Unregister_RegisteredGuid_RemovesEntry()
        {
            // Arrange
            var resolver = new EntityPortraitResolver();
            var guid = Guid.NewGuid();
            resolver.Register(guid, CreateSprite());

            // Act
            resolver.Unregister(guid);

            // Assert
            Assert.IsFalse(resolver.TryGetPortrait(guid, out _));
        }

        [Test]
        public void Clear_RemovesAllExplicitEntries()
        {
            // Arrange
            var resolver = new EntityPortraitResolver();
            var guid = Guid.NewGuid();
            resolver.Register(guid, CreateSprite());

            // Act
            resolver.Clear();

            // Assert
            Assert.IsFalse(resolver.TryGetPortrait(guid, out _));
        }

        // -------------------------------------------------------------------
        // Resolución lazy del player
        // -------------------------------------------------------------------

        [Test]
        public void TryGetPortrait_PlayerGuid_ResolvesLazyFromCurrentHero()
        {
            // Arrange
            var heroSprite = CreateSprite();
            var playerService = new FakePlayerService
            {
                PlayerGuid = Guid.NewGuid(),
                CurrentHero = CreateHero(heroSprite),
            };
            var resolver = new EntityPortraitResolver(playerService);

            // Act — sin Register previo del player.
            bool found = resolver.TryGetPortrait(playerService.PlayerGuid, out var result);

            // Assert
            Assert.IsTrue(found);
            Assert.AreSame(heroSprite, result);
        }

        [Test]
        public void TryGetPortrait_PlayerGuidWithExplicitRegistration_DictWins()
        {
            // Arrange
            var heroSprite = CreateSprite();
            var overrideSprite = CreateSprite();
            var playerService = new FakePlayerService
            {
                PlayerGuid = Guid.NewGuid(),
                CurrentHero = CreateHero(heroSprite),
            };
            var resolver = new EntityPortraitResolver(playerService);
            resolver.Register(playerService.PlayerGuid, overrideSprite);

            // Act
            resolver.TryGetPortrait(playerService.PlayerGuid, out var result);

            // Assert
            Assert.AreSame(overrideSprite, result,
                "El registro explícito debe pisar la resolución lazy del hero.");
        }

        [Test]
        public void TryGetPortrait_PlayerGuidWithoutHeroPortrait_ReturnsFalse()
        {
            // Arrange
            var playerService = new FakePlayerService
            {
                PlayerGuid = Guid.NewGuid(),
                CurrentHero = CreateHero(portrait: null),
            };
            var resolver = new EntityPortraitResolver(playerService);

            // Act
            bool found = resolver.TryGetPortrait(playerService.PlayerGuid, out _);

            // Assert
            Assert.IsFalse(found);
        }

        [Test]
        public void TryGetPortrait_NullPlayerService_ReturnsFalseWithoutThrowing()
        {
            // Arrange
            var resolver = new EntityPortraitResolver(playerService: null);

            // Act + Assert
            Assert.DoesNotThrow(() => resolver.TryGetPortrait(Guid.NewGuid(), out _));
            Assert.IsFalse(resolver.TryGetPortrait(Guid.NewGuid(), out _));
        }

        [Test]
        public void TryGetPortrait_EmptyGuid_NeverResolvesLazy()
        {
            // Arrange — PlayerGuid sin setear (= Guid.Empty) más hero con portrait:
            // Guid.Empty nunca debe matchear el lazy path.
            var playerService = new FakePlayerService
            {
                CurrentHero = CreateHero(CreateSprite()),
            };
            var resolver = new EntityPortraitResolver(playerService);

            // Act
            bool found = resolver.TryGetPortrait(Guid.Empty, out _);

            // Assert
            Assert.IsFalse(found);
        }
    }
}
