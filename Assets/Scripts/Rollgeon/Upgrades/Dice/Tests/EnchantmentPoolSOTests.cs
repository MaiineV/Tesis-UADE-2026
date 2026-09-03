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

        // ---- Multiplicador de peso de malditos (Moneda Maldita) -------------

        [Test]
        public void Roll_FavoursCursedEntries_WhenWeightServiceMultiplies()
        {
            // Arrange — 1 maldito y 1 normal, peso 1 c/u; multiplicador enorme para
            // que el sesgo sea inequívoco con seed fijo.
            var cursed = MakeEnchantment("cursed", DiceType.D6);
            cursed.EditorSetCategory(EnchantmentCategory.Caos);
            var normal = MakeEnchantment("normal", DiceType.D6);
            var pool = MakePool(
                new WeightedEnchantment { Enchantment = cursed, Weight = 1f },
                new WeightedEnchantment { Enchantment = normal, Weight = 1f }
            );
            var rng = new Random(42);

            global::Patterns.ServiceLocator.Clear();
            var service = new EnchantmentWeightModifierService();
            service.Register();
            service.Register("moneda.maldita", 1000f);
            try
            {
                // Act
                int cursedCount = 0;
                for (int i = 0; i < 200; i++)
                {
                    if (pool.Roll(rng, DiceType.D6, floorDepth: 0) == cursed) cursedCount++;
                }

                // Assert — esperado ~199.8/200; umbral holgado y determinista por seed.
                Assert.GreaterOrEqual(cursedCount, 190,
                    $"con peso x1000 el maldito debería dominar (salió {cursedCount}/200)");
            }
            finally
            {
                service.Dispose();
                global::Patterns.ServiceLocator.Clear();
            }
        }

        [Test]
        public void Roll_UsesRawWeights_WhenWeightServiceMissing()
        {
            // Arrange — degrade permisivo: sin servicio, pesos crudos, ambos salen.
            var cursed = MakeEnchantment("cursed", DiceType.D6);
            cursed.EditorSetCategory(EnchantmentCategory.Caos);
            var normal = MakeEnchantment("normal", DiceType.D6);
            var pool = MakePool(
                new WeightedEnchantment { Enchantment = cursed, Weight = 1f },
                new WeightedEnchantment { Enchantment = normal, Weight = 1f }
            );
            var rng = new Random(42);
            global::Patterns.ServiceLocator.Clear();
            var seen = new HashSet<EnchantmentSO>();

            // Act
            for (int i = 0; i < 100; i++) seen.Add(pool.Roll(rng, DiceType.D6, floorDepth: 0));

            // Assert
            Assert.IsTrue(seen.Contains(cursed));
            Assert.IsTrue(seen.Contains(normal));
        }

        [Test]
        public void Roll_DoesNotAffectNonCursedEntries_WhenServiceRegistered()
        {
            // Arrange — pool 100% normal: el multiplicador registrado no cambia nada.
            var a = MakeEnchantment("a", DiceType.D6);
            var b = MakeEnchantment("b", DiceType.D6);
            var pool = MakePool(
                new WeightedEnchantment { Enchantment = a, Weight = 1f },
                new WeightedEnchantment { Enchantment = b, Weight = 1f }
            );

            global::Patterns.ServiceLocator.Clear();
            var service = new EnchantmentWeightModifierService();
            service.Register();
            service.Register("moneda.maldita", 1000f);
            try
            {
                // Act — misma seed con y sin multiplicador da la misma secuencia,
                // porque ningún entry es maldito y los pesos efectivos no cambian.
                var withService = RollSequence(pool, seed: 99, count: 50);
                service.Unregister("moneda.maldita");
                var withoutMult = RollSequence(pool, seed: 99, count: 50);

                // Assert
                CollectionAssert.AreEqual(withoutMult, withService);
            }
            finally
            {
                service.Dispose();
                global::Patterns.ServiceLocator.Clear();
            }
        }

        private List<EnchantmentSO> RollSequence(EnchantmentPoolSO pool, int seed, int count)
        {
            var rng = new Random(seed);
            var sequence = new List<EnchantmentSO>(count);
            for (int i = 0; i < count; i++) sequence.Add(pool.Roll(rng, DiceType.D6, floorDepth: 0));
            return sequence;
        }
        // ---- Roll con filtro (slot garantizado de Moneda Maldita) -------------

        private static void SetCursed(EnchantmentSO ench)
        {
            var field = typeof(EnchantmentSO).GetField("_capabilities", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(ench, new List<IEnchantmentCapability> { new CapCursed() });
        }

        [Test]
        public void Roll_WithFilter_OnlyReturnsMatchingEntries()
        {
            var a = MakeEnchantment("a");
            var b = MakeEnchantment("b");
            var c = MakeEnchantment("c");
            var cursed = MakeEnchantment("cursed");
            SetCursed(cursed);
            var pool = MakePool(
                new WeightedEnchantment { Enchantment = a, Weight = 10f },
                new WeightedEnchantment { Enchantment = b, Weight = 10f },
                new WeightedEnchantment { Enchantment = c, Weight = 10f },
                new WeightedEnchantment { Enchantment = cursed, Weight = 1f });
            var rng = new Random(7);

            for (int i = 0; i < 50; i++)
            {
                var picked = pool.Roll(rng, new[] { DiceType.D6 }, 0, null, EnchantmentPoolSO.IsCursedForPool);
                Assert.AreSame(cursed, picked, "roll #" + i + " devolvio un no-maldito con filtro solo-malditos");
            }
        }

        [Test]
        public void Roll_WithFilter_NoMatch_ReturnsNullEvenOnExcludeFallback()
        {
            var a = MakeEnchantment("a");
            var pool = MakePool(new WeightedEnchantment { Enchantment = a, Weight = 1f });

            // El fallback que ignora el exclude también tiene que respetar el filtro.
            var picked = pool.Roll(new Random(1), new[] { DiceType.D6 }, 0,
                exclude: new HashSet<EnchantmentSO> { a }, filter: EnchantmentPoolSO.IsCursedForPool);

            Assert.IsNull(picked);
        }

        [Test]
        public void IsCursedForPool_CapCursedOrCaosCategory_True_OtherwiseFalse()
        {
            var plain = MakeEnchantment("plain");
            var capped = MakeEnchantment("capped");
            SetCursed(capped);
            var caos = MakeEnchantment("caos");
            typeof(EnchantmentSO).GetField("_category", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(caos, EnchantmentCategory.Caos);

            Assert.IsFalse(EnchantmentPoolSO.IsCursedForPool(plain));
            Assert.IsTrue(EnchantmentPoolSO.IsCursedForPool(capped));
            Assert.IsTrue(EnchantmentPoolSO.IsCursedForPool(caos));
            Assert.IsFalse(EnchantmentPoolSO.IsCursedForPool(null));
        }
    }
}
