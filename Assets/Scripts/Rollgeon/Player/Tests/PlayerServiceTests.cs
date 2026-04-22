using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Player.Tests
{
    [TestFixture]
    public class PlayerServiceTests
    {
        private PlayerService _service;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _service = new PlayerService();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
            ServiceLocator.Clear();
        }

        [Test]
        public void Register_AddsToServiceLocator()
        {
            ServiceLocator.AddService<IPlayerService>(_service, ServiceScope.Global);

            var resolved = ServiceLocator.GetService<IPlayerService>();
            Assert.AreSame(_service, resolved);
        }

        [Test]
        public void PlayerGuid_DefaultIsEmpty()
        {
            Assert.AreEqual(Guid.Empty, _service.PlayerGuid);
        }

        [Test]
        public void RunId_DefaultIsEmpty()
        {
            Assert.AreEqual(Guid.Empty, _service.RunId);
        }

        [Test]
        public void CurrentHero_DefaultIsNull()
        {
            Assert.IsNull(_service.CurrentHero);
        }

        [Test]
        public void SetPlayer_PopulatesAllProperties()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            var runId = Guid.NewGuid();

            _service.SetPlayer(hero, runId);

            Assert.AreEqual(runId, _service.RunId);
            Assert.AreSame(hero, _service.CurrentHero);
            Assert.AreNotEqual(Guid.Empty, _service.PlayerGuid);

            UnityEngine.Object.DestroyImmediate(hero);
        }

        [Test]
        public void SetPlayer_GeneratesNonEmptyPlayerGuid()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();

            _service.SetPlayer(hero, Guid.NewGuid());

            Assert.AreNotEqual(Guid.Empty, _service.PlayerGuid);

            UnityEngine.Object.DestroyImmediate(hero);
        }

        [Test]
        public void SetPlayer_FiresOnPlayerSetWithHero()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            ClassHeroSO received = null;
            _service.OnPlayerSet += h => received = h;

            _service.SetPlayer(hero, Guid.NewGuid());

            Assert.AreSame(hero, received);

            UnityEngine.Object.DestroyImmediate(hero);
        }

        [Test]
        public void ClearPlayer_ResetsAllToDefaults()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _service.SetPlayer(hero, Guid.NewGuid());

            _service.ClearPlayer();

            Assert.AreEqual(Guid.Empty, _service.PlayerGuid);
            Assert.AreEqual(Guid.Empty, _service.RunId);
            Assert.IsNull(_service.CurrentHero);

            UnityEngine.Object.DestroyImmediate(hero);
        }

        [Test]
        public void ClearPlayer_FiresOnPlayerCleared()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _service.SetPlayer(hero, Guid.NewGuid());

            bool fired = false;
            _service.OnPlayerCleared += () => fired = true;

            _service.ClearPlayer();

            Assert.IsTrue(fired);

            UnityEngine.Object.DestroyImmediate(hero);
        }

        [Test]
        public void SetPlayer_Twice_OverwritesPreviousState()
        {
            var hero1 = ScriptableObject.CreateInstance<ClassHeroSO>();
            var hero2 = ScriptableObject.CreateInstance<ClassHeroSO>();
            var runId1 = Guid.NewGuid();
            var runId2 = Guid.NewGuid();

            _service.SetPlayer(hero1, runId1);
            var guid1 = _service.PlayerGuid;

            _service.SetPlayer(hero2, runId2);

            Assert.AreSame(hero2, _service.CurrentHero);
            Assert.AreEqual(runId2, _service.RunId);
            Assert.AreNotEqual(guid1, _service.PlayerGuid);

            UnityEngine.Object.DestroyImmediate(hero1);
            UnityEngine.Object.DestroyImmediate(hero2);
        }

        [Test]
        public void SetPlayer_NullHero_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _service.SetPlayer(null, Guid.NewGuid()));
        }

        [Test]
        public void SetPlayer_HeroWithoutDiceBag_LeavesDiceBagNull()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            // hero.StartingDiceBagRef queda null por default.

            _service.SetPlayer(hero, Guid.NewGuid());

            Assert.IsNull(_service.DiceBag);

            UnityEngine.Object.DestroyImmediate(hero);
        }

        [Test]
        public void SetPlayer_HeroWithDiceBag_ClonesIt()
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType>
            {
                DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6,
            };

            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            hero.StartingDiceBagRef = bag;

            _service.SetPlayer(hero, Guid.NewGuid());

            Assert.IsNotNull(_service.DiceBag);
            Assert.AreNotSame(bag, _service.DiceBag, "DiceBag debería ser un clon, no el asset original.");
            CollectionAssert.AreEqual(bag.Dice, _service.DiceBag.Dice);

            UnityEngine.Object.DestroyImmediate(hero);
            UnityEngine.Object.DestroyImmediate(bag);
            UnityEngine.Object.DestroyImmediate(_service.DiceBag);
        }

        [Test]
        public void SetDiceBag_OverridesActiveBag()
        {
            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            _service.SetPlayer(hero, Guid.NewGuid());

            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D8, DiceType.D8, DiceType.D6, DiceType.D6, DiceType.D6 };

            _service.SetDiceBag(bag);

            Assert.AreSame(bag, _service.DiceBag);

            UnityEngine.Object.DestroyImmediate(hero);
            UnityEngine.Object.DestroyImmediate(bag);
        }
    }
}
