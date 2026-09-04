using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice.Readers;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// <see cref="ReadTilesTraversed"/>: casillas del movimiento × multiplicador, tope por
    /// turno sin estado (a partir del acumulado del contexto) y stacking GDD (solo la
    /// primera copia lee; cada copia extra sube el tope).
    /// </summary>
    [TestFixture]
    public class ReadTilesTraversedTests
    {
        private readonly List<Object> _created = new List<Object>();
        private DiceEnchantmentService _svc;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
        }

        private static EffectContext Ctx(int tiles, int tilesThisTurn, int enchSlot = 0)
        {
            return new EffectContext
            {
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = new EnchantmentScratch(),
                    Slot = new EnchantmentSlotRef(DiceType.D6, EnchantmentSlotRef.MovementDieSlot, enchSlot),
                    Channel = Upgrades.ScratchChannel.DiceEnchantment,
                    TilesTraversed = tiles,
                    TilesTraversedThisTurn = tilesThisTurn,
                },
            };
        }

        private EnchantmentSO MakeMovementEnchantment(string id)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);
            typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, id);
            typeof(EnchantmentSO).GetField("_category", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(ench, EnchantmentCategory.Movimiento);
            return ench;
        }

        private void RegisterServiceWithMovementLane(params EnchantmentSO[] lane)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6 };
            _created.Add(bag);
            _svc = new DiceEnchantmentService(config: null);
            _svc.InitializeFromBag(bag);
            foreach (var e in lane) _svc.Bag.AddEnchantment(EnchantmentSlotRef.MovementDieSlot, e);
            ServiceLocator.AddService<IDiceEnchantmentService>(_svc, ServiceScope.Global);
        }

        [Test]
        public void Read_NoCap_ReturnsTilesTimesMultiplier()
        {
            var reader = new ReadTilesTraversed { Multiplier = 2 };

            Assert.AreEqual(8, reader.Read(Ctx(tiles: 4, tilesThisTurn: 4)));
        }

        [Test]
        public void Read_OutsideMovementHook_ReturnsZero()
        {
            var reader = new ReadTilesTraversed();

            Assert.AreEqual(0, reader.Read(Ctx(tiles: 0, tilesThisTurn: 0)));
            Assert.AreEqual(0, reader.Read(new EffectContext()));
        }

        [Test]
        public void Read_WithCap_CountsOnlyTheTilesUnderTheTurnCap()
        {
            // Baluarte móvil: 4 casillas ⇒ 4; otras 4 en el mismo turno ⇒ solo 2 (tope 6);
            // otras 3 ⇒ 0.
            var reader = new ReadTilesTraversed { CapPerTurn = 6 };

            Assert.AreEqual(4, reader.Read(Ctx(tiles: 4, tilesThisTurn: 4)));
            Assert.AreEqual(2, reader.Read(Ctx(tiles: 4, tilesThisTurn: 8)));
            Assert.AreEqual(0, reader.Read(Ctx(tiles: 3, tilesThisTurn: 11)));
        }

        [Test]
        public void Read_TwoCopies_OnlyFirstCopyReadsAndCapGrows()
        {
            var ench = MakeMovementEnchantment("ench.baluarte");
            RegisterServiceWithMovementLane(ench, ench);
            var reader = new ReadTilesTraversed { CapPerTurn = 6, CapPerExtraCopy = 3 };

            // 9 casillas de una: tope 6 + 3 = 9 — la primera copia lee todo, la segunda nada.
            Assert.AreEqual(9, reader.Read(Ctx(tiles: 9, tilesThisTurn: 9, enchSlot: 0)));
            Assert.AreEqual(0, reader.Read(Ctx(tiles: 9, tilesThisTurn: 9, enchSlot: 1)));
        }

        [Test]
        public void Read_TombstonedFirstCopy_SecondCopyBecomesTheReader()
        {
            var ench = MakeMovementEnchantment("ench.baluarte");
            RegisterServiceWithMovementLane(ench, ench);
            _svc.Bag.SetEnchantmentAt(EnchantmentSlotRef.MovementDieSlot, 0, null);
            var reader = new ReadTilesTraversed { CapPerTurn = 6, CapPerExtraCopy = 3 };

            Assert.AreEqual(6, reader.Read(Ctx(tiles: 9, tilesThisTurn: 9, enchSlot: 1)));
        }
    }
}
