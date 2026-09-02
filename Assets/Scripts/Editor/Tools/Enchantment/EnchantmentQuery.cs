using System.Collections.Generic;
using System.Linq;
using Rollgeon.Upgrades.Dice;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Capa de solo-lectura sobre los assets de encantamiento — espejo de
    /// <c>ItemQuery</c>. Línea dura: nada acá llama <c>Undo.RecordObject</c> ni
    /// <c>SetDirty</c>. Cada query expone dos formas: una zero-arg que escanea disco vía
    /// <c>AssetDatabase</c>, y una overload pura sobre <c>IEnumerable</c> para tests.
    /// </summary>
    public static partial class EnchantmentQuery
    {
        /// <summary>Todos los <see cref="EnchantmentSO"/> del proyecto, ordenados por asset path (determinista).</summary>
        public static IReadOnlyList<EnchantmentSO> GetAll()
        {
            var result = new List<(string path, EnchantmentSO so)>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(EnchantmentSO)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (so != null) result.Add((path, so));
            }
            result.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
            return result.Select(t => t.so).ToList();
        }

        /// <summary>
        /// Una categoría con sus encantamientos — la "vista de familia" del dominio: acá
        /// la agrupación es la categoría del GDD, no escalones de variantes.
        /// </summary>
        public sealed class CategoryGroup
        {
            public EnchantmentCategory Category { get; }
            public IReadOnlyList<EnchantmentSO> Enchantments { get; }

            public CategoryGroup(EnchantmentCategory category, IReadOnlyList<EnchantmentSO> enchantments)
            {
                Category = category;
                Enchantments = enchantments;
            }
        }

        /// <summary>Agrupados por categoría, escaneando disco.</summary>
        public static IReadOnlyList<CategoryGroup> GetByCategory() => GetByCategory(GetAll());

        /// <summary>
        /// Forma pura de <see cref="GetByCategory()"/>. Grupos en el orden del enum
        /// (None primero si hay sin clasificar — que se vea), encantamientos por
        /// DisplayName Ordinal.
        /// </summary>
        public static IReadOnlyList<CategoryGroup> GetByCategory(IEnumerable<EnchantmentSO> enchantments)
        {
            var byCategory = new Dictionary<EnchantmentCategory, List<EnchantmentSO>>();
            foreach (var ench in enchantments ?? Enumerable.Empty<EnchantmentSO>())
            {
                if (ench == null) continue;
                if (!byCategory.TryGetValue(ench.Category, out var list))
                    byCategory[ench.Category] = list = new List<EnchantmentSO>();
                list.Add(ench);
            }

            var groups = new List<CategoryGroup>();
            foreach (var kv in byCategory.OrderBy(kv => (int)kv.Key))
            {
                kv.Value.Sort((a, b) => string.CompareOrdinal(LabelOf(a), LabelOf(b)));
                groups.Add(new CategoryGroup(kv.Key, kv.Value));
            }
            return groups;
        }

        /// <summary><c>DisplayName</c> con fallback al nombre del asset.</summary>
        public static string LabelOf(EnchantmentSO enchantment)
        {
            if (enchantment == null) return "(null)";
            return string.IsNullOrEmpty(enchantment.DisplayName) ? enchantment.name : enchantment.DisplayName;
        }
    }
}
