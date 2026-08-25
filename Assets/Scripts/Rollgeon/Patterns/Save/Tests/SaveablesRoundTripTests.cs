using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Patterns.Save;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Economy;
using Rollgeon.Run;
using Rollgeon.Upgrades;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Patterns.Save.Tests
{
    /// <summary>
    /// Round-trips capture → restore de los saveables sumados para el resume:
    /// oro, pasivas de combo, enchantments del bag y atributos del player.
    /// </summary>
    [TestFixture]
    public class SaveablesRoundTripTests
    {
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            SaveSystem.ResetForTests();
            foreach (var asset in _assets)
                if (asset != null) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        // ====================================================================
        // Oro
        // ====================================================================

        [Test]
        public void EconomyService_CaptureRestore_RoundTripsGold()
        {
            var economy = new EconomyService(10);
            try
            {
                economy.Add(55);

                var captured = economy.CaptureState();

                var reborn = new EconomyService(10);
                try
                {
                    reborn.RestoreState(captured);
                    Assert.AreEqual(65, reborn.CurrentGold);
                }
                finally { reborn.Dispose(); }
            }
            finally { economy.Dispose(); }
        }

        [Test]
        public void EconomyService_OnRunStart_ResetsToStartingGold()
        {
            var economy = new EconomyService(10);
            try
            {
                economy.Add(90);

                EventManager.Trigger(EventName.OnRunStart, Guid.NewGuid(), "test");

                Assert.AreEqual(10, economy.CurrentGold, "run nueva resetea el oro (fix del leak entre runs)");
            }
            finally { economy.Dispose(); }
        }

        // ====================================================================
        // Pasivas de combo
        // ====================================================================

        [Test]
        public void RunComboPassivesState_CaptureRestore_RoundTripsWithStacking()
        {
            var parPassive = MakePassive("upg.par_bonus", "combo.par");
            var trioPassive = MakePassive("upg.trio_bonus", "combo.trio");
            var byId = new Dictionary<string, ComboPassiveSO>
            {
                { "upg.par_bonus", parPassive },
                { "upg.trio_bonus", trioPassive },
            };

            var state = new RunComboPassivesState(id => byId.TryGetValue(id, out var p) ? p : null);
            state.Add(parPassive);
            state.Add(parPassive); // stacking
            state.Add(trioPassive);

            var captured = state.CaptureState();

            var reborn = new RunComboPassivesState(id => byId.TryGetValue(id, out var p) ? p : null);
            reborn.RestoreState(captured);

            Assert.AreEqual(3, reborn.TotalCount);
            Assert.AreEqual(2, reborn.Get("combo.par").Count);
            Assert.AreEqual(1, reborn.Get("combo.trio").Count);
        }

        [Test]
        public void RunComboPassivesState_Restore_UnknownId_DiscardsWithWarning()
        {
            var state = new RunComboPassivesState(_ => null);

            state.RestoreState(new List<string> { "upg.borrado_del_pool" });

            Assert.AreEqual(0, state.TotalCount);
        }

        // ====================================================================
        // Enchantments del bag
        // ====================================================================

        [Test]
        public void RuntimeDiceBag_CaptureRestore_RoundTripsEnchantmentsAndCounters()
        {
            var ench = MakeEnchantment("upg.ench_explode");
            var byId = new Dictionary<string, EnchantmentSO> { { "upg.ench_explode", ench } };
            var dice = new List<DiceType> { DiceType.D6, DiceType.D6 };

            var bag = new RuntimeDiceBag(dice, id => byId.TryGetValue(id, out var e) ? e : null);
            int assignedIndex = bag.AddEnchantment(0, ench);
            Assert.AreEqual(0, assignedIndex, "primer append al dado debe quedar en el índice 0");
            var slotRef = new EnchantmentSlotRef(DiceType.D6, 0, 0);
            bag.IncrementCounter(slotRef, "altar_reroll_count", 3);
            bag.IncrementDieCounter(0, "altar_roll_count", 2);

            var captured = bag.CaptureState();

            var reborn = new RuntimeDiceBag(dice, id => byId.TryGetValue(id, out var e) ? e : null);
            reborn.RestoreState(captured);

            Assert.AreSame(ench, reborn.GetEnchantmentAt(0, 0));
            Assert.AreEqual(3, reborn.GetCounter(slotRef, "altar_reroll_count"),
                "la economía del altar (contador de rerolls) viaja con el save");
            Assert.AreEqual(2, reborn.GetDieCounter(0, "altar_roll_count"),
                "el counter per-dado (rolls acumulados del altar) también viaja con el save");
        }

        // ====================================================================
        // Atributos del player
        // ====================================================================

        [Test]
        public void PlayerAttributes_CaptureRestore_RoundTripsValuesAndRunModifiers()
        {
            var carrier = Guid.NewGuid();
            var attrs = MakePlayerAttributes(hp: 30);
            var health = attrs.GetAttribute<Health>();
            health.Value = 17;

            // Upgrade de personaje: modifier Run-lifetime (patrón PlayerStatGrants).
            var runMod = new Modifier<int>(4, ModifierOperation.Add, 0, carrier, Guid.Empty,
                ModifierDirection.Intrinsic, ModifierLifetime.Run, EventName.OnTurnFinished);
            health.AddModifier<int>(runMod);
            // Combat-scoped: NO debe sobrevivir el resume.
            var encounterMod = new Modifier<int>(-2, ModifierOperation.Add, 0, carrier, Guid.Empty,
                ModifierDirection.Intrinsic, ModifierLifetime.Encounter, EventName.OnTurnFinished);
            health.AddModifier<int>(encounterMod);

            var saveable = new PlayerAttributesSaveable(attrs);
            var captured = saveable.CaptureState();

            var freshAttrs = MakePlayerAttributes(hp: 30);
            var rebornSaveable = new PlayerAttributesSaveable(freshAttrs);
            rebornSaveable.RestoreState(captured);

            var rebornHealth = freshAttrs.GetAttribute<Health>();
            Assert.AreEqual(17, rebornHealth.Value, "HP actual restaurado");
            Assert.AreEqual(1, rebornHealth.GetRawModifiers().Count,
                "solo el modifier Run sobrevive; Encounter queda afuera");
            Assert.AreEqual(4, rebornHealth.GetRawModifiers()[0].Amount);
            Assert.AreEqual(runMod.ModifierId, rebornHealth.GetRawModifiers()[0].ModifierId);
        }

        [Test]
        public void PlayerAttributes_Restore_LegacyEnergyEntry_IsDiscardedGracefully()
        {
            // Feature#0050: los saves viejos traen entries Energy/MaxEnergy que el
            // player actual ya no tiene — se descartan con warning, sin romper el
            // resto del snapshot.
            var captured = new PlayerAttributesSnapshot();
            captured.Stats.Add(new PlayerStatEntry { Stat = "Health", Value = 17 });
            captured.Stats.Add(new PlayerStatEntry { Stat = "Energy", Value = 2 });
            captured.Stats.Add(new PlayerStatEntry { Stat = "MaxEnergy", Value = 4 });

            var fresh = MakePlayerAttributes(hp: 30);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                new PlayerAttributesSaveable(fresh).RestoreState(captured);
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            Assert.AreEqual(17, fresh.GetAttribute<Health>().Value);
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private ComboPassiveSO MakePassive(string upgradeId, string targetComboId)
        {
            var passive = ScriptableObject.CreateInstance<ComboPassiveSO>();
            _assets.Add(passive);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(passive, upgradeId);
            typeof(ComboPassiveSO).GetField("_targetComboId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(passive, targetComboId);
            return passive;
        }

        private EnchantmentSO MakeEnchantment(string upgradeId)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _assets.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(ench, upgradeId);
            return ench;
        }

        private static ModifiableAttributes MakePlayerAttributes(int hp)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            attrs.SetAttribute<Attack>(new Attack(0));
            attrs.SetAttribute<Speed>(new Speed(3));
            return attrs;
        }
    }
}
