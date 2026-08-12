using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Items;
using UnityEngine;

namespace Rollgeon.UI.ChestReveal.Tests
{
    [TestFixture]
    public class ChestReelBuilderTests
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

        private ItemSO NewItem(string id)
        {
            var item = ScriptableObject.CreateInstance<ItemSO>();
            item.ItemId = id;
            _assets.Add(item);
            return item;
        }

        private static IReadOnlyList<ChestReelCellData> Build(
            ChestReelCellData winner, IReadOnlyList<ItemSO> pool,
            int totalCells = 40, int winnerIndex = 36, int goldPerMille = 150, int seed = 42)
        {
            return ChestReelBuilder.BuildStrip(
                winner, pool, ItemRarity.Rare, totalCells, winnerIndex,
                goldPerMille, goldFillerMin: 5, goldFillerMax: 20, new System.Random(seed));
        }

        [Test]
        public void BuildStrip_ShouldPlaceWinnerAtExactIndex()
        {
            // Arrange
            var winnerItem = NewItem("reel.winner");
            var pool = new[] { NewItem("reel.a"), NewItem("reel.b") };

            // Act
            var strip = Build(ChestReelCellData.ForItem(winnerItem, isWinner: true), pool);

            // Assert
            Assert.AreEqual(40, strip.Count);
            Assert.IsTrue(strip[36].IsWinner);
            Assert.AreSame(winnerItem, strip[36].Item);
            for (int i = 0; i < strip.Count; i++)
            {
                if (i != 36) Assert.IsFalse(strip[i].IsWinner, $"celda {i} marcada winner");
            }
        }

        [Test]
        public void BuildStrip_ShouldClampWinnerIndex_IntoRange()
        {
            // Arrange
            var winner = ChestReelCellData.ForGold(10, ItemRarity.Common, isWinner: true);

            // Act
            var strip = Build(winner, new ItemSO[0], totalCells: 10, winnerIndex: 99);

            // Assert
            Assert.IsTrue(strip[9].IsWinner);
        }

        [Test]
        public void BuildStrip_ShouldAvoidIdenticalAdjacentItems_WhenPoolHasVariety()
        {
            // Arrange
            var pool = new[] { NewItem("reel.a"), NewItem("reel.b"), NewItem("reel.c") };
            var winner = ChestReelCellData.ForItem(pool[0], isWinner: true);

            // Act — sin fillers de oro para forzar solo ítems.
            var strip = Build(winner, pool, goldPerMille: 0);

            // Assert
            for (int i = 1; i < strip.Count; i++)
            {
                if (strip[i].Item == null || strip[i - 1].Item == null) continue;
                Assert.AreNotSame(strip[i].Item, strip[i - 1].Item,
                    $"celdas {i - 1} y {i} repiten el mismo ítem");
            }
        }

        [Test]
        public void BuildStrip_ShouldDegradeToGoldFillers_WhenPoolIsEmpty()
        {
            // Arrange
            var winner = ChestReelCellData.ForGold(50, ItemRarity.Legendary, isWinner: true);

            // Act
            var strip = Build(winner, new ItemSO[0], totalCells: 20, winnerIndex: 15);

            // Assert — todas las celdas son oro, montos dentro del rango de filler.
            for (int i = 0; i < strip.Count; i++)
            {
                Assert.IsTrue(strip[i].IsGold);
                if (i == 15) continue;
                Assert.GreaterOrEqual(strip[i].GoldAmount, 5);
                Assert.LessOrEqual(strip[i].GoldAmount, 20);
            }
            Assert.AreEqual(50, strip[15].GoldAmount);
        }

        [Test]
        public void BuildStrip_ShouldNotAddGoldFillers_WhenPerMilleIsZero()
        {
            // Arrange
            var pool = new[] { NewItem("reel.a"), NewItem("reel.b") };
            var winner = ChestReelCellData.ForItem(pool[0], isWinner: true);

            // Act
            var strip = Build(winner, pool, goldPerMille: 0);

            // Assert
            foreach (var cell in strip) Assert.IsFalse(cell.IsGold);
        }

        [Test]
        public void BuildStrip_ShouldTolerateSingleItemPool()
        {
            // Arrange — 1 solo ítem: la regla de adyacencia no puede aplicarse.
            var only = NewItem("reel.only");
            var winner = ChestReelCellData.ForItem(only, isWinner: true);

            // Act
            var strip = Build(winner, new[] { only }, goldPerMille: 0, totalCells: 10, winnerIndex: 8);

            // Assert — no crashea y todas las celdas son el ítem.
            Assert.AreEqual(10, strip.Count);
            foreach (var cell in strip) Assert.AreSame(only, cell.Item);
        }

        [Test]
        public void BuildStrip_ShouldBeDeterministic_ForSameSeed()
        {
            // Arrange
            var pool = new[] { NewItem("reel.a"), NewItem("reel.b"), NewItem("reel.c") };
            var winner = ChestReelCellData.ForItem(pool[1], isWinner: true);

            // Act
            var first = Build(winner, pool, seed: 1234);
            var second = Build(winner, pool, seed: 1234);

            // Assert
            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i].Item, second[i].Item, $"celda {i}");
                Assert.AreEqual(first[i].GoldAmount, second[i].GoldAmount, $"celda {i}");
            }
        }
    }
}
