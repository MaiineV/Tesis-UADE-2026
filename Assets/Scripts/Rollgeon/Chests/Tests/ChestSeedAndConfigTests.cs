using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
using Rollgeon.Shop;
using UnityEngine;

namespace Rollgeon.Chests.Tests
{
    [TestFixture]
    public class ChestSeedAndConfigTests
    {
        private readonly List<Object> _assets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets)
            {
                if (asset != null) Object.DestroyImmediate(asset);
            }
            _assets.Clear();
        }

        [Test]
        public void Derive_ShouldBeDeterministic_ForSameFloorSeedAndCell()
        {
            // Arrange
            var cell = new Vector2Int(3, -2);

            // Act
            int first = ChestSeed.Derive(1234, cell);
            int second = ChestSeed.Derive(1234, cell);

            // Assert
            Assert.AreEqual(first, second);
        }

        [Test]
        public void Derive_ShouldDiffer_ForDifferentCells()
        {
            // Arrange + Act
            int a = ChestSeed.Derive(1234, new Vector2Int(0, 0));
            int b = ChestSeed.Derive(1234, new Vector2Int(1, 0));

            // Assert
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Derive_ShouldDecorrelateFromShopSeed_ForSameInputs()
        {
            // Arrange
            var cell = new Vector2Int(2, 5);

            // Act
            int chestSeed = ChestSeed.Derive(777, cell);
            int shopSeed = ShopManagerService.DeriveShopSeed(777, cell);

            // Assert — mismo input, seeds distintos (el salt separa los streams).
            Assert.AreNotEqual(shopSeed, chestSeed);
        }

        [Test]
        public void RollTier_ShouldRespectWeights_WhenFloorEntryApplies()
        {
            // Arrange — piso 2 con 100% Legendary.
            var config = NewConfig();
            config.TierWeightsByFloor.Add(new ChestFloorTierWeights
            {
                FloorNumber = 2,
                Common = 0f,
                Uncommon = 0f,
                Rare = 0f,
                Legendary = 1f
            });
            var rng = new System.Random(42);

            // Act + Assert
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(ItemRarity.Legendary, config.RollTier(rng, floorNumber: 2));
            }
        }

        [Test]
        public void RollTier_ShouldPickHighestApplicableFloorEntry()
        {
            // Arrange — piso 1: solo Common; piso 3+: solo Rare. En piso 5 aplica la de 3.
            var config = NewConfig();
            config.TierWeightsByFloor.Add(new ChestFloorTierWeights { FloorNumber = 1, Common = 1f });
            config.TierWeightsByFloor.Add(new ChestFloorTierWeights
            {
                FloorNumber = 3,
                Common = 0f,
                Rare = 1f
            });
            var rng = new System.Random(7);

            // Act + Assert
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(ItemRarity.Rare, config.RollTier(rng, floorNumber: 5));
            }
        }

        [Test]
        public void RollTier_ShouldFallBackToUniform_WhenTableIsEmpty()
        {
            // Arrange
            var config = NewConfig();
            var rng = new System.Random(2024);
            var seen = new HashSet<ItemRarity>();

            // Act
            for (int i = 0; i < 200; i++) seen.Add(config.RollTier(rng, floorNumber: 1));

            // Assert — uniforme sobre 4 tiers: con 200 muestras salen todos.
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void GetTierDef_ShouldReturnMatchingTier()
        {
            // Arrange
            var config = NewConfig();
            config.Tiers.Add(new ChestTierDef { Tier = ItemRarity.Common, MaxHP = 20 });
            config.Tiers.Add(new ChestTierDef { Tier = ItemRarity.Rare, MaxHP = 40 });

            // Act
            var def = config.GetTierDef(ItemRarity.Rare);

            // Assert
            Assert.AreEqual(40, def.MaxHP);
        }

        private ChestConfigSO NewConfig()
        {
            var config = ScriptableObject.CreateInstance<ChestConfigSO>();
            _assets.Add(config);
            return config;
        }
    }
}
