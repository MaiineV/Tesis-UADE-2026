using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura del guard de divergencia del <see cref="EnchantedDiceRoller"/>
    /// (BUG-012). El decorator deriva las caras de cada dado del
    /// <see cref="RuntimeDiceBag"/> del enchantment service; si ese runtime no
    /// coincide con la bolsa que se tira, un dado quedaría clampeado al rango
    /// equivocado (un D20 saliendo 1-6). El guard detecta la divergencia y tira
    /// el rango real del dado.
    /// </summary>
    [TestFixture]
    public class EnchantedDiceRollerTests
    {
        private const int Seed = 1234567;
        private const int RollSamples = 300;

        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            foreach (var obj in _created)
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            _created.Clear();
        }

        // ----------------------------------------------------------------
        // Regresión BUG-012
        // ----------------------------------------------------------------

        [Test]
        public void RollAll_RuntimeBagDivergesFromRolledBag_RollsRealDieRange()
        {
            // Arrange — el RuntimeDiceBag quedó cacheado contra el fallback 5×D6
            // (lo que pasaba en BUG-012), pero la bolsa real arranca con un D20.
            RegisterServiceInitializedWith(DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6);
            var roller = new EnchantedDiceRoller(new ZeroRoller(), Seed);
            var rolledBag = MakeBag(DiceType.D20, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6);

            // Act — muchas tiradas (seed fijo → determinístico) y miramos el slot 0.
            int maxSlot0 = 0;
            int minSlot0 = int.MaxValue;
            for (int i = 0; i < RollSamples; i++)
            {
                var result = roller.RollAll(rolledBag);
                maxSlot0 = Mathf.Max(maxSlot0, result[0]);
                minSlot0 = Mathf.Min(minSlot0, result[0]);
            }

            // Assert — el D20 real puede superar 6 (no quedó clampeado al D6 del runtime)
            // y nunca excede su rango real.
            Assert.Greater(maxSlot0, 6,
                "El D20 quedó clampeado al rango del RuntimeDiceBag (D6) — el guard de BUG-012 no actuó.");
            Assert.LessOrEqual(maxSlot0, DiceType.D20.MaxFace());
            Assert.GreaterOrEqual(minSlot0, 1);
        }

        [Test]
        public void RollAll_RuntimeBagMatchesRolledBag_RespectsEachDieRange()
        {
            // Arrange — caso normal post-fix: runtime y bolsa tirada coinciden slot a slot.
            RegisterServiceInitializedWith(DiceType.D20, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6);
            var roller = new EnchantedDiceRoller(new ZeroRoller(), Seed);
            var rolledBag = MakeBag(DiceType.D20, DiceType.D6, DiceType.D6, DiceType.D6, DiceType.D6);

            // Act
            int maxSlot0 = 0;
            var allValues = new List<int[]>();
            for (int i = 0; i < RollSamples; i++)
            {
                var result = roller.RollAll(rolledBag);
                allValues.Add(result);
                maxSlot0 = Mathf.Max(maxSlot0, result[0]);
            }

            // Assert — slot 0 (D20) usa rango completo; slots 1-4 (D6) nunca superan 6.
            Assert.Greater(maxSlot0, 6, "El D20 alineado debería poder superar 6.");
            foreach (var roll in allValues)
            {
                Assert.That(roll[0], Is.InRange(1, DiceType.D20.MaxFace()));
                for (int slot = 1; slot < roll.Length; slot++)
                    Assert.That(roll[slot], Is.InRange(1, DiceType.D6.MaxFace()),
                        $"El D6 del slot {slot} salió fuera de rango.");
            }
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private void RegisterServiceInitializedWith(params DiceType[] runtimeDice)
        {
            var svc = new DiceEnchantmentService(config: null);
            svc.InitializeFromBag(MakeBag(runtimeDice));
            ServiceLocator.AddService<IDiceEnchantmentService>(svc, ServiceScope.Run);
        }

        private DiceBagSO MakeBag(params DiceType[] dice)
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.name = "TestBag";
            bag.Dice = new List<DiceType>(dice);
            _created.Add(bag);
            return bag;
        }

        /// <summary>
        /// Inner roller que siempre devuelve ceros — así, si el decorator llegara a
        /// delegar al inner (no debería con el service ready), el test fallaría de
        /// forma evidente (valores 0, fuera de todo rango de dado).
        /// </summary>
        private sealed class ZeroRoller : IDiceRoller
        {
            public int[] RollAll(DiceBagSO bag) => new int[bag.Dice.Count];

            public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
                => new int[bag.Dice.Count];
        }
    }
}
