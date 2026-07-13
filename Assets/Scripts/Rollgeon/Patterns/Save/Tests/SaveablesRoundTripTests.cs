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
            Assert.IsTrue(bag.SetEnchantmentAt(0, 0, ench), "el D6 debe tener al menos un slot");
            var slotRef = new EnchantmentSlotRef(DiceType.D6, 0, 0);
            bag.IncrementCounter(slotRef, "altar_reroll_count", 3);

            var captured = bag.CaptureState();

            var reborn = new RuntimeDiceBag(dice, id => byId.TryGetValue(id, out var e) ? e : null);
            reborn.RestoreState(captured);

            Assert.AreSame(ench, reborn.GetEnchantmentAt(0, 0));
            Assert.AreEqual(3, reborn.GetCounter(slotRef, "altar_reroll_count"),
                "la economía del altar (contador de rerolls) viaja con el save");
        }

        // ====================================================================
        // Atributos del player
        // ====================================================================

        [Test]
        public void PlayerAttributes_CaptureRestore_RoundTripsValuesAndRunModifiers()
        {
            var carrier = Guid.NewGuid();
            var attrs = MakePlayerAttributes(hp: 30, energy: 5);
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

            var freshAttrs = MakePlayerAttributes(hp: 30, energy: 5);
            var rebornSaveable = new PlayerAttributesSaveable(freshAttrs);
            rebornSaveable.RestoreState(captured);

            var rebornHealth = freshAttrs.GetAttribute<Health>();
            Assert.AreEqual(17, rebornHealth.Value, "HP actual restaurado");
            Assert.AreEqual(1, rebornHealth.GetRawModifiers().Count,
                "solo el modifier Run sobrevive; Encounter queda afuera");
            Assert.AreEqual(4, rebornHealth.GetRawModifiers()[0].Amount);
            Assert.AreEqual(runMod.ModifierId, rebornHealth.GetRawModifiers()[0].ModifierId);

            Assert.AreEqual(5, freshAttrs.GetAttribute<Energy>().Value);
        }

        [Test]
        public void PlayerAttributes_Restore_EnergyMissing_CreatesIt()
        {
            var attrs = MakePlayerAttributes(hp: 30, energy: 5);
            attrs.GetAttribute<Energy>().Value = 2;
            var captured = new PlayerAttributesSaveable(attrs).CaptureState();

            // Player fresco sin Energy todavía (EnergyService no corrió).
            var fresh = new ModifiableAttributes();
            fresh.EnsureInitialized();
            fresh.SetAttribute<Health>(new Health(30));
            fresh.SetAttribute<Attack>(new Attack(0));
            fresh.SetAttribute<Speed>(new Speed(3));

            new PlayerAttributesSaveable(fresh).RestoreState(captured);

            Assert.IsTrue(fresh.HasAttribute<Energy>());
            Assert.AreEqual(2, fresh.GetAttribute<Energy>().Value);
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

        private static ModifiableAttributes MakePlayerAttributes(int hp, int energy)
        {
            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            attrs.SetAttribute<Energy>(new Energy(energy));
            attrs.SetAttribute<Attack>(new Attack(0));
            attrs.SetAttribute<Speed>(new Speed(3));
            return attrs;
        }
    }
}
