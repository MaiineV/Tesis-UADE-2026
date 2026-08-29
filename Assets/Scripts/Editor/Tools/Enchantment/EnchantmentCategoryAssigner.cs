using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Enchantment
{
    /// <summary>
    /// Asigna la <see cref="EnchantmentCategory"/> a todos los assets de encantamiento.
    /// Los malditos (<c>IsCursed()</c>) van a Maldición automáticamente; el resto sale
    /// del diccionario. Idempotente — reejecutar pisa con el mismo valor.
    /// </summary>
    /// <remarks>
    /// Los assets son Odin (SerializationNodes): la asignación va por el setter
    /// editor-only de <see cref="EnchantmentSO"/> + SetDirty + SaveAssets, nunca
    /// editando el YAML a mano. Ajustes finos: el Enchantment Editor
    /// (<c>Tools → Enchantment Editor</c>) muestra el campo Category.
    /// </remarks>
    public static class EnchantmentCategoryAssigner
    {
        // Clasificación de lectura por efecto (ver descripciones en el seeder).
        // Los cursed NO necesitan entrada — IsCursed() los manda a Maldición.
        private static readonly Dictionary<string, EnchantmentCategory> ByUpgradeId = new()
        {
            ["ench.afilado"] = EnchantmentCategory.Ataque,
            ["ench.ancla"] = EnchantmentCategory.Control,
            ["ench.avaro"] = EnchantmentCategory.Economia,
            ["ench.caras_centrales"] = EnchantmentCategory.Control,
            ["ench.cargado"] = EnchantmentCategory.Control,
            ["ench.codicioso"] = EnchantmentCategory.Economia,
            ["ench.comodin"] = EnchantmentCategory.Control,
            ["ench.escalador"] = EnchantmentCategory.Control,
            ["ench.escudado"] = EnchantmentCategory.Defensa,
            ["ench.extremos"] = EnchantmentCategory.Control,
            ["ench.fortaleza"] = EnchantmentCategory.Defensa,
            ["ench.fragil"] = EnchantmentCategory.Maldicion,
            ["ench.gemelo"] = EnchantmentCategory.Ataque,
            ["ench.gold_on_roll"] = EnchantmentCategory.Economia,
            ["ench.impar"] = EnchantmentCategory.Control,
            ["ench.invertido"] = EnchantmentCategory.Control,
            ["ench.lento"] = EnchantmentCategory.Maldicion,
            ["ench.mercader"] = EnchantmentCategory.Economia,
            ["ench.mimetico"] = EnchantmentCategory.Control,
            ["ench.mitad_inferior"] = EnchantmentCategory.Maldicion,
            ["ench.mitad_superior"] = EnchantmentCategory.Ataque,
            ["ench.multiplo_de_3"] = EnchantmentCategory.Control,
            ["ench.no_primo"] = EnchantmentCategory.Control,
            ["ench.only_evens"] = EnchantmentCategory.Control,
            ["ench.oxidado"] = EnchantmentCategory.Maldicion,
            ["ench.par"] = EnchantmentCategory.Control,
            ["ench.parity_gamble"] = EnchantmentCategory.Ataque,
            ["ench.pesado"] = EnchantmentCategory.Ataque,
            ["ench.primo"] = EnchantmentCategory.Control,
            ["ench.resonante"] = EnchantmentCategory.Ataque,
            ["ench.sediento"] = EnchantmentCategory.Maldicion,
            ["ench.torpe"] = EnchantmentCategory.Maldicion,
            ["ench.volatil"] = EnchantmentCategory.Ataque,
        };

        [MenuItem("Rollgeon/Enchantments/Assign Categories")]
        public static void AssignAll()
        {
            int assigned = 0, missing = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EnchantmentSO", new[] { "Assets/Rollgeon" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench == null) continue;

                EnchantmentCategory category;
                if (ench.IsCursed())
                {
                    category = EnchantmentCategory.Maldicion;
                }
                else if (!ByUpgradeId.TryGetValue(ench.UpgradeId ?? string.Empty, out category))
                {
                    Debug.LogWarning($"[EnchCategories] '{ench.UpgradeId}' ({path}) sin entrada en el " +
                                     "diccionario — queda None. Sumarlo y reejecutar.", ench);
                    missing++;
                    continue;
                }

                ench.EditorSetCategory(category);
                EditorUtility.SetDirty(ench);
                assigned++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EnchCategories] {assigned} asignados, {missing} sin mapear.");
        }
    }
}
