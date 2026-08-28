using NUnit.Framework;
using Rollgeon.Items;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item.Tests
{
    /// <summary>
    /// Covers global id-collision detection and "an invalid spec fails before writing anything"
    /// (task scope for A3 — writing to the live Content/ShopPool/ItemCatalog assets is deliberately
    /// out of scope here, since a test must not mutate those project assets).
    /// </summary>
    public sealed class ItemAuthoringTests
    {
        const string TestFolder = "Assets/Rollgeon/Items/__ItemAuthoringTests";
        ItemSO _probe;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.CreateFolder("Assets/Rollgeon/Items", "__ItemAuthoringTests");

            _probe = ScriptableObject.CreateInstance<ItemSO>();
            _probe.ItemId = "test.probe.item";
            _probe.DisplayName = "Test Probe Item";
            AssetDatabase.CreateAsset(_probe, TestFolder + "/Item_TestProbeItem.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (_probe != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_probe));
            if (AssetDatabase.IsValidFolder(TestFolder))
                AssetDatabase.DeleteAsset(TestFolder);
        }

        [Test]
        public void IsIdAvailable_UnusedId_ReturnsTrueWithNullOwner()
        {
            var available = ItemAuthoring.IsIdAvailable("nobody.has.this.id.yet", out var owner);

            Assert.IsTrue(available);
            Assert.IsNull(owner);
        }

        [Test]
        public void IsIdAvailable_UsedId_ReturnsFalseWithOwner()
        {
            var available = ItemAuthoring.IsIdAvailable("test.probe.item", out var owner);

            Assert.IsFalse(available);
            Assert.AreEqual(_probe, owner);
        }

        [Test]
        public void IsIdAvailable_EmptyOrNullId_ReturnsFalse()
        {
            Assert.IsFalse(ItemAuthoring.IsIdAvailable(null, out _));
            Assert.IsFalse(ItemAuthoring.IsIdAvailable(string.Empty, out _));
        }

        [Test]
        public void CreateItem_EmptyDisplayName_FailsWithoutCreatingAnAsset()
        {
            var spec = new ItemCreationSpec
            {
                DisplayName = "",
                Rarity = ItemRarity.Common,
                Type = ItemType.Passive,
            };

            var before = AssetDatabase.FindAssets("t:ItemSO").Length;
            var result = ItemAuthoring.CreateItem(spec);
            var after = AssetDatabase.FindAssets("t:ItemSO").Length;

            Assert.IsFalse(result.Success);
            Assert.IsNotEmpty(result.Errors);
            Assert.IsNull(result.Item);
            Assert.AreEqual(before, after, "A failed validation must not create any asset.");
        }

        [Test]
        public void CreateItem_DuplicateId_FailsAndNamesTheOwner()
        {
            var spec = new ItemCreationSpec
            {
                DisplayName = "Test Probe Item",
                Rarity = ItemRarity.Common,
                Type = ItemType.Passive,
            };

            var result = ItemAuthoring.CreateItem(spec);

            Assert.IsFalse(result.Success);
            StringAssert.Contains("test.probe.item", string.Join(" ", result.Errors));
        }

        [Test]
        public void CreateFamily_TwoVariantsWithSameDerivedId_FailsBeforeWritingEither()
        {
            var spec = new ItemFamilyCreationSpec
            {
                FamilyId = "test.family",
                Type = ItemType.Passive,
                Variants = new[]
                {
                    new ItemFamilyVariantSpec { DisplayName = "Botas del Viento", Rarity = ItemRarity.Common },
                    new ItemFamilyVariantSpec { DisplayName = "Botas del Viento", Rarity = ItemRarity.Rare },
                },
            };

            var before = AssetDatabase.FindAssets("t:ItemSO").Length;
            var result = ItemAuthoring.CreateFamily(spec);
            var after = AssetDatabase.FindAssets("t:ItemSO").Length;

            Assert.IsFalse(result.Success);
            Assert.AreEqual(before, after);
        }
    }
}
