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
            AddStandardTiers(config);
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
            AddStandardTiers(config);
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
            AddStandardTiers(config);
            var rng = new System.Random(2024);
            var seen = new HashSet<ItemRarity>();

            // Act
            for (int i = 0; i < 200; i++) seen.Add(config.RollTier(rng, floorNumber: 1));

            // Assert — uniforme sobre los 4 tiers configurados: con 200 muestras salen todos.
            Assert.AreEqual(4, seen.Count);
        }

        [Test]
        public void RollTier_ShouldNeverPickGod_WhenNoTierDefConfigured()
        {
            // Arrange — regresión del bug de item-editor-spec.md §5: agregar God al
            // enum NO debe hacer que un cofre sin tier Dios configurado (Tiers ni
            // TierWeightsByFloor) pueda salir sorteado como Dios — antes caía en el
            // default: de WeightFor y ahora, además, ConfiguredTierValues() ni
            // siquiera lo ofrece como candidato.
            var config = NewConfig();
            AddStandardTiers(config); // Common/Uncommon/Rare/Legendary — sin God, a propósito.
            var rng = new System.Random(99);

            // Act + Assert — sin TierWeightsByFloor ⇒ ejercita el fallback uniforme,
            // el camino que rompía antes de acotar el universo a Tiers.
            for (int i = 0; i < 500; i++)
            {
                Assert.AreNotEqual(ItemRarity.God, config.RollTier(rng, floorNumber: 1));
            }
        }

        [Test]
        public void WeightFor_God_ShouldNotFallBackToCommon()
        {
            // Arrange — el bug concreto: antes, cualquier ItemRarity no listado en el
            // switch (God incluido) devolvía el peso de Common en silencio.
            var weights = new ChestFloorTierWeights { Common = 5f, God = 0f };

            // Act + Assert
            Assert.AreEqual(0f, weights.WeightFor(ItemRarity.God));
            Assert.AreNotEqual(weights.WeightFor(ItemRarity.Common), weights.WeightFor(ItemRarity.God));
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

        // Espeja el seed real de ChestSetupTools.CreateAssets(): 4 tiers, sin God
        // (item-editor-spec.md §5.3 — el tier Dios no está confirmado como
        // mecánica de cofre). RollTier depende de Tiers para saber qué puede
        // sortear, así que los tests de RollTier necesitan esto poblado.
        private static void AddStandardTiers(ChestConfigSO config)
        {
            config.Tiers.Add(new ChestTierDef { Tier = ItemRarity.Common });
            config.Tiers.Add(new ChestTierDef { Tier = ItemRarity.Uncommon });
            config.Tiers.Add(new ChestTierDef { Tier = ItemRarity.Rare });
            config.Tiers.Add(new ChestTierDef { Tier = ItemRarity.Legendary });
        }
    }
}
