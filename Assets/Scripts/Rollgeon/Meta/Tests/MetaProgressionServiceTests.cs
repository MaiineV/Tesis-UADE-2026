using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Meta.Conditions;
using UnityEngine;

namespace Rollgeon.Meta.Tests
{
    /// <summary>
    /// Tests del <see cref="MetaProgressionService"/> (#164): semántica de pool
    /// base (estado inicial Guerrero + D3/D4/D6 sin seed), unlock idempotente con
    /// write-through, contadores persistentes (racha resetea al morir, clases
    /// acumulan) y round-trip del snapshot.
    /// </summary>
    [TestFixture]
    public class MetaProgressionServiceTests
    {
        private MetaProgressionService _service;
        private InMemoryMetaSaveStore _store;
        private UnlockCatalogSO _catalog;
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void Setup()
        {
            _store = new InMemoryMetaSaveStore();
            _catalog = ScriptableObject.CreateInstance<UnlockCatalogSO>();
            _assets.Add(_catalog);

            _service = new MetaProgressionService();
            _service.ConfigureForTests(_store, _catalog);
        }

        [TearDown]
        public void Teardown()
        {
            TypedEvent<UnlockAchievedPayload>.Clear();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
            ServiceLocator.Clear();
        }

        private UnlockDefinitionSO AddDefinition(
            string unlockId, UnlockableCategory category, string targetId,
            IUnlockCondition condition = null,
            UnlockOutcomeFilter appliesTo = UnlockOutcomeFilter.Won)
        {
            var def = ScriptableObject.CreateInstance<UnlockDefinitionSO>();
            def.UnlockId = unlockId;
            def.Category = category;
            def.TargetId = targetId;
            def.DisplayName = unlockId;
            def.AppliesTo = appliesTo;
            def.Condition = condition;
            _assets.Add(def);

            _catalog.AddEntry(def);
            _service.ConfigureForTests(_store, _catalog); // invalida el cache de Definitions
            return def;
        }

        // ── Estado inicial / pool base ──────────────────────────

        [Test]
        public void IsAvailable_UngatedContent_TrueOnFirstSession()
        {
            // El estado inicial no necesita seed: contenido sin definición = pool base.
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.Dice, "D3"));
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.Dice, "D4"));
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.Dice, "D6"));
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.HeroClass, "Warrior"));
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.ShopItem, "item.base"));
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.Enchantment, "ench.base"));
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.SpecialRoom, "Shop01"));
        }

        [Test]
        public void IsAvailable_GatedContent_FalseUntilUnlocked()
        {
            var def = AddDefinition("unlock.dice.d8", UnlockableCategory.Dice, "D8");

            Assert.IsFalse(_service.IsAvailable(UnlockableCategory.Dice, "D8"));

            _service.TryUnlock(def, duringRun: false);

            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.Dice, "D8"));
        }

        [Test]
        public void IsAvailable_GatedClass_FalseAcrossAllFiveCategories()
        {
            AddDefinition("unlock.class.berserker", UnlockableCategory.HeroClass, "Berserker");
            AddDefinition("unlock.shop.bomb", UnlockableCategory.ShopItem, "item.bomb");
            AddDefinition("unlock.ench.evens", UnlockableCategory.Enchantment, "ench.only_evens");
            AddDefinition("unlock.room.casino", UnlockableCategory.SpecialRoom, "Casino01");

            Assert.IsFalse(_service.IsAvailable(UnlockableCategory.HeroClass, "Berserker"));
            Assert.IsFalse(_service.IsAvailable(UnlockableCategory.ShopItem, "item.bomb"));
            Assert.IsFalse(_service.IsAvailable(UnlockableCategory.Enchantment, "ench.only_evens"));
            Assert.IsFalse(_service.IsAvailable(UnlockableCategory.SpecialRoom, "Casino01"));
        }

        [Test]
        public void IsAvailable_SameIdDifferentCategory_IsIndependent()
        {
            AddDefinition("unlock.dice.d8", UnlockableCategory.Dice, "D8");

            Assert.IsFalse(_service.IsAvailable(UnlockableCategory.Dice, "D8"));
            Assert.IsTrue(_service.IsAvailable(UnlockableCategory.SpecialRoom, "D8"));
        }

        // ── TryUnlock ───────────────────────────────────────────

        [Test]
        public void TryUnlock_FirstTime_SavesImmediatelyAndRaisesEvent()
        {
            var def = AddDefinition("unlock.dice.d8", UnlockableCategory.Dice, "D8");
            UnlockAchievedPayload? received = null;
            TypedEvent<UnlockAchievedPayload>.Subscribe(p => received = p);
            int savesBefore = _store.SaveCount;

            bool unlocked = _service.TryUnlock(def, duringRun: true);

            Assert.IsTrue(unlocked);
            Assert.AreEqual(savesBefore + 1, _store.SaveCount, "El unlock debe persistirse inmediatamente");
            Assert.IsNotNull(received);
            Assert.AreEqual("unlock.dice.d8", received.Value.UnlockId);
            Assert.AreEqual("D8", received.Value.TargetId);
            Assert.IsTrue(received.Value.DuringRun);
        }

        [Test]
        public void TryUnlock_SecondTime_IsIdempotent()
        {
            var def = AddDefinition("unlock.dice.d8", UnlockableCategory.Dice, "D8");
            _service.TryUnlock(def, duringRun: false);
            int savesBefore = _store.SaveCount;

            bool unlockedAgain = _service.TryUnlock(def, duringRun: false);

            Assert.IsFalse(unlockedAgain);
            Assert.AreEqual(savesBefore, _store.SaveCount);
        }

        [Test]
        public void IsDefinitionCompleted_AfterUnlock_ReturnsTrue()
        {
            var def = AddDefinition("unlock.dice.d8", UnlockableCategory.Dice, "D8");

            Assert.IsFalse(_service.IsDefinitionCompleted(def));
            _service.TryUnlock(def, duringRun: false);
            Assert.IsTrue(_service.IsDefinitionCompleted(def));
        }

        // ── Contadores persistentes ─────────────────────────────

        [Test]
        public void RecordRunCompleted_Wins_IncrementStreak()
        {
            _service.RecordRunCompleted(won: true, classId: "Warrior");
            _service.RecordRunCompleted(won: true, classId: "Warrior");

            Assert.AreEqual(2, _service.ConsecutiveWins);
        }

        [Test]
        public void RecordRunCompleted_Loss_ResetsStreakButKeepsClasses()
        {
            _service.RecordRunCompleted(won: true, classId: "Warrior");
            _service.RecordRunCompleted(won: true, classId: "Berserker");

            _service.RecordRunCompleted(won: false, classId: "Gambler");

            Assert.AreEqual(0, _service.ConsecutiveWins, "La racha (consistencia) resetea al morir");
            Assert.AreEqual(3, _service.ClassesPlayed.Count, "Las clases jugadas (acumulación) no resetean");
        }

        [Test]
        public void RecordRunCompleted_RepeatedClass_CountsOnce()
        {
            _service.RecordRunCompleted(won: true, classId: "Warrior");
            _service.RecordRunCompleted(won: false, classId: "Warrior");

            Assert.AreEqual(1, _service.ClassesPlayed.Count);
        }

        // ── Persistencia round-trip ─────────────────────────────

        [Test]
        public void Persistence_SnapshotRoundTrip_PreservesEverything()
        {
            var def = AddDefinition("unlock.dice.d8", UnlockableCategory.Dice, "D8");
            _service.TryUnlock(def, duringRun: false);
            _service.RecordRunCompleted(won: true, classId: "Warrior");

            // Un "segundo arranque" hidratado desde el mismo store.
            var reloaded = new MetaProgressionService();
            reloaded.ConfigureForTests(_store, _catalog);
            reloaded.State.RestoreState(_store.Load());

            Assert.IsTrue(reloaded.IsAvailable(UnlockableCategory.Dice, "D8"));
            Assert.IsTrue(reloaded.IsDefinitionCompleted(def));
            Assert.AreEqual(1, reloaded.ConsecutiveWins);
            CollectionAssert.Contains(reloaded.ClassesPlayed, "Warrior");
        }

        [Test]
        public void MetaProgressionState_RestoreNull_ResetsToInitial()
        {
            var state = new MetaProgressionState();
            state.UnlockedTargetKeys.Add("Dice:D8");
            state.ConsecutiveWins = 5;

            state.RestoreState(null);

            Assert.AreEqual(0, state.UnlockedTargetKeys.Count);
            Assert.AreEqual(0, state.ConsecutiveWins);
        }
    }
}
