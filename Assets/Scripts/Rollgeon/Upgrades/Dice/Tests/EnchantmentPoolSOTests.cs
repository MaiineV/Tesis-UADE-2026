using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Dice;
using UnityEngine;
using Random = System.Random;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura de <see cref="EnchantmentPoolSO.Roll"/> — los tres filtros (peso,
    /// minFloorDepth, dice compatibility) + el exclude opcional.
    /// </summary>
    [TestFixture]
    public class EnchantmentPoolSOTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ---- Helpers --------------------------------------------------------

        private EnchantmentSO MakeEnchantment(string id, params DiceType[] allowedTypes)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id;
            _created.Add(ench);

            SetUpgradeId(ench, id);
            SetAllowedDiceTypes(ench, new List<DiceType>(allowedTypes));
            return ench;
        }

        private EnchantmentPoolSO MakePool(params WeightedEnchantment[] entries)
        {
            var pool = ScriptableObject.CreateInstance<EnchantmentPoolSO>();
            pool.Entries = new List<WeightedEnchantment>(entries);
            _created.Add(pool);
            return pool;
        }

        private static void SetUpgradeId(EnchantmentSO ench, string id)
        {
            var field = typeof(UpgradeSO).GetField("_upgradeId", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(ench, id);
        }

        private static void SetAllowedDiceTypes(EnchantmentSO ench, List<DiceType> types)
        {
            var field = typeof(EnchantmentSO).GetField("_allowedDiceTypes", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(ench, types);
        }

        // ---- Tests ----------------------------------------------------------

        [Test]
        public void Roll_EmptyPool_ReturnsNull()
        {
            var pool = MakePool();
            var rng = new Random(42);

            var result = pool.Roll(rng, DiceType.D6, floorDepth: 0);

            Assert.IsNull(result);
        }

        [Test]
        public void Roll_NoCompatibleWithTargetType_ReturnsNull()
        {
            var ench = MakeEnchantment("only_d20", DiceType.D20);
            var pool = MakePool(new WeightedEnchantment { Enchantment = ench, Weight = 1f });
            var rng = new Random(42);

            var result = pool.Roll(rng, DiceType.D6, floorDepth: 0);

            Assert.IsNull(result);
        }

        [Test]
        public void Roll_EmptyAllowedDiceTypes_IsCompatibleWithEveryDie()
        {
            var ench = MakeEnchantment("universal"); // empty AllowedDiceTypes
            var pool = MakePool(new WeightedEnchantment { Enchantment = ench, Weight = 1f });
            var rng = new Random(42);

            var resultD3 = pool.Roll(rng, DiceType.D3, floorDepth: 0);
            var resultD20 = pool.Roll(rng, DiceType.D20, floorDepth: 0);

            Assert.AreSame(ench, resultD3);
            Assert.AreSame(ench, resultD20);
        }

        [Test]
        public void Roll_FloorDepthBelowMin_FiltersOutEntry()
        {
            var early = MakeEnchantment("early", DiceType.D6);
            var late = MakeEnchantment("late", DiceType.D6);
            var pool = MakePool(
                new WeightedEnchantment { Enchantment = early, Weight = 1f, MinFloorDepth = 0 },
                new WeightedEnchantment { Enchantment = late,  Weight = 1f, MinFloorDepth = 3 }
            );
            var rng = new Random(42);

            // En floor 0 solo `early` es elegible — múltiples rolls confirman.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreSame(early, pool.Roll(rng, DiceType.D6, floorDepth: 0));
            }
        }

        [Test]
        public void Roll_WeightZero_SkipsEntry()
        {
            var active = MakeEnchantment("active", DiceType.D6);
            var disabled = MakeEnchantment("disabled", DiceType.D6);
            var pool = MakePool(
                new WeightedEnchantment { Enchantment = active,   Weight = 1f },
                new WeightedEnchantment { Enchantment = disabled, Weight = 0f }
            );
            var rng = new Random(42);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreSame(active, pool.Roll(rng, DiceType.D6, floorDepth: 0));
            }
        }

        [Test]
        public void Roll_ExcludeContainsAllCompatible_FallsBackToReturnSome()
        {
            // El pool tiene solo "a" para D6; lo excluimos. Debería fallback
            // y devolver "a" (mejor algo que nada).
            var only = MakeEnchantment("only", DiceType.D6);
            var pool = MakePool(new WeightedEnchantment { Enchantment = only, Weight = 1f });
            var rng = new Random(42);

            var result = pool.Roll(rng, DiceType.D6, floorDepth: 0, exclude: new HashSet<EnchantmentSO> { only });

            Assert.AreSame(only, result, "fallback debe devolver el único elegible cuando exclude lo bloquea");
        }

        [Test]
        public void Roll_ExcludeAllowsAlternative_PrefersNonExcluded()
        {
            var a = MakeEnchantment("a", DiceType.D6);
            var b = MakeEnchantment("b", DiceType.D6);
            var pool = MakePool(
                new WeightedEnchantment { Enchantment = a, Weight = 1f },
                new WeightedEnchantment { Enchantment = b, Weight = 1f }
            );
            var rng = new Random(42);

            for (int i = 0; i < 20; i++)
            {
                var result = pool.Roll(rng, DiceType.D6, floorDepth: 0, exclude: new HashSet<EnchantmentSO> { a });
                Assert.AreSame(b, result, "con `a` excluida, todos los rolls deberían devolver `b`");
            }
        }
    }
}
