using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Items;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Combos.Triggers.Concretes;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Editor.Tools.Polymorphic.Tests
{
    /// <summary>
    /// Pins the two questions the whole authoring engine rests on: where a picker goes, and where
    /// the drawer is allowed to take over from Odin.
    /// </summary>
    public sealed class PolymorphicMemberScannerTests
    {
        [SetUp]
        public void SetUp() => PolymorphicMemberScanner.ClearCache();

        static string[] NamesOf(IReadOnlyList<PolymorphicMember> members)
        {
            var names = new string[members.Count];
            for (int i = 0; i < members.Count; i++) names[i] = members[i].Name;
            return names;
        }

        // ---- picker slots --------------------------------------------------

        [Test]
        public void Scan_EffectData_FindsItsThreePolymorphicSlots()
        {
            var names = NamesOf(PolymorphicMemberScanner.Scan(typeof(EffectData)));

            CollectionAssert.AreEquivalent(
                new[] { "PreConditions", "Effects", "TargetSelector" }, names);
        }

        [Test]
        public void Scan_EnchantmentSO_FindsFilterAndTriggers()
        {
            var names = NamesOf(PolymorphicMemberScanner.Scan(typeof(EnchantmentSO)));

            CollectionAssert.AreEquivalent(new[] { "_faceFilter", "_triggers" }, names);
        }

        [Test]
        public void Scan_ComboPassiveSO_FindsReaderAndTriggers()
        {
            var names = NamesOf(PolymorphicMemberScanner.Scan(typeof(ComboPassiveSO)));

            CollectionAssert.AreEquivalent(new[] { "_flatDamageBonus", "_extraTriggers" }, names);
        }

        [Test]
        public void Scan_MarksListsAsLists()
        {
            foreach (var m in PolymorphicMemberScanner.Scan(typeof(EffectData)))
            {
                if (m.Name == "TargetSelector") Assert.IsFalse(m.IsList, "TargetSelector is a single slot");
                else Assert.IsTrue(m.IsList, $"{m.Name} is a list");
            }
        }

        // ---- containers to walk into ---------------------------------------

        [Test]
        public void BlockMembersOf_ItemSO_FindsHooksAndOnActivate()
        {
            var names = NamesOf(PolymorphicMemberScanner.BlockMembersOf(typeof(ItemSO)));

            CollectionAssert.AreEquivalent(new[] { "PassiveHooks", "OnActivate" }, names);
        }

        [Test]
        public void BlockMembersOf_EffChain_FindsPhases()
        {
            var names = NamesOf(PolymorphicMemberScanner.BlockMembersOf(typeof(EffChain)));

            CollectionAssert.AreEquivalent(new[] { "Phases" }, names);
        }

        /// <summary>
        /// The case that falsifies an "EffectData-generic" drawer: a combo passive only reaches
        /// EffectData through a trigger, so a drawer keyed on EffectData would need to special-case
        /// the bridge. Keyed on hidden pickers, it falls out for free.
        /// </summary>
        [Test]
        public void BlockMembersOf_ExecuteEffectsOnEvent_ReachesEffectDataTransitively()
        {
            var names = NamesOf(PolymorphicMemberScanner.BlockMembersOf(typeof(ExecuteEffectsOnEvent)));

            CollectionAssert.AreEquivalent(new[] { "Effects" }, names);
        }

        /// <summary>
        /// Guards the rule that keeps the working tools looking the way they do. SelectionSettings
        /// holds an ISelectionCountReader, which Odin draws fine — if "holds a polymorphic slot"
        /// were the test instead of "holds a *hidden* picker", every effect would start rendering
        /// field-by-field and the enemy tool's layout would silently change.
        /// </summary>
        [Test]
        public void BlockMembersOf_PlainEffects_FindNothing_SoOdinKeepsDrawingThemWhole()
        {
            Assert.IsEmpty(PolymorphicMemberScanner.BlockMembersOf(typeof(EffHeal)), "EffHeal");
            Assert.IsEmpty(PolymorphicMemberScanner.BlockMembersOf(typeof(EffDealDamage)), "EffDealDamage");
            Assert.IsEmpty(PolymorphicMemberScanner.BlockMembersOf(typeof(EffAddShield)), "EffAddShield");
        }

        [Test]
        public void HasHiddenPickerDeep_SelectionSettings_IsFalse()
        {
            Assert.IsFalse(
                PolymorphicMemberScanner.HasHiddenPickerDeep(typeof(Rollgeon.Effects.Selection.SelectionSettings)),
                "ISelectionCountReader has no [HideReferenceObjectPicker], so Odin can author it.");
        }

        [Test]
        public void HasHiddenPickerDeep_EffectData_IsTrue()
        {
            Assert.IsTrue(PolymorphicMemberScanner.HasHiddenPickerDeep(typeof(EffectData)),
                "BasePreCondition carries the attribute, so Odin cannot author it.");
        }

        /// <summary>EffChain -> ChainPhase -> EffectData -> IEffect -> EffChain is legal at runtime.</summary>
        [Test]
        public void HasHiddenPickerDeep_Terminates_OnRecursiveChain()
        {
            Assert.IsTrue(PolymorphicMemberScanner.HasHiddenPickerDeep(typeof(EffChain)));
            Assert.IsTrue(PolymorphicMemberScanner.HasHiddenPickerDeep(typeof(ChainPhase)));
        }

        [Test]
        public void Scan_IsCached_ReturnsSameInstance()
        {
            var first = PolymorphicMemberScanner.Scan(typeof(EffectData));
            var second = PolymorphicMemberScanner.Scan(typeof(EffectData));

            Assert.AreSame(first, second);
        }
    }
}
