using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Enchantment
{
    /// <summary>
    /// Asigna la <see cref="EnchantmentCategory"/> a todos los assets de encantamiento
    /// según la taxonomía del GDD ("Listado encantamientos", 2026-09): Caos / Recursos /
    /// Ataque / Control / Movimiento. Idempotente — reejecutar pisa con el mismo valor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// La categoría y lo maldito son ejes ORTOGONALES: <c>CapCursed</c> decide color de
    /// título y peso de ruleta; la categoría sale solo de este diccionario (ej.
    /// <c>ench.mitad_inferior</c> es maldito pero Control — restringe caras).
    /// </para>
    /// <para>
    /// Los assets son Odin (SerializationNodes): la asignación va por el setter
    /// editor-only de <see cref="EnchantmentSO"/> + SetDirty + SaveAssets, nunca
    /// editando el YAML a mano. Las altas nuevas por <c>EnchantmentAuthoring</c> ya
    /// nacen con categoría — esto es la herramienta de reparación masiva.
    /// </para>
    /// </remarks>
    public static class EnchantmentCategoryAssigner
    {
        // Fuente: GDD "Listado encantamientos" (secciones 1-5). Los ids que el GDD no
        // lista (afilado, escalador, gold_on_roll, invertido, mimetico, no_primo,
        // only_evens, parity_gamble, resonante, torpe) se asignan por la definición de
        // cada categoría: restricción de caras / valor / combos → Control; genera oro →
        // Recursos; daño condicionado → Ataque; maldito puro → Caos.
        private static readonly Dictionary<string, EnchantmentCategory> ByUpgradeId = new()
        {
            // 🩸 Caos — efectos negativos a cambio de una ganancia.
            ["ench.fragil"] = EnchantmentCategory.Caos,
            ["ench.lento"] = EnchantmentCategory.Caos,
            ["ench.oxidado"] = EnchantmentCategory.Caos,
            ["ench.sediento"] = EnchantmentCategory.Caos,
            ["ench.torpe"] = EnchantmentCategory.Caos,
            ["ench.vampiro"] = EnchantmentCategory.Caos,

            // 💰 Recursos — generan oro/escudo al usar el dado.
            ["ench.avaro"] = EnchantmentCategory.Recursos,
            ["ench.codicioso"] = EnchantmentCategory.Recursos,
            ["ench.el_caudal"] = EnchantmentCategory.Recursos,
            ["ench.escudado"] = EnchantmentCategory.Recursos,
            ["ench.fortaleza"] = EnchantmentCategory.Recursos,
            ["ench.gold_on_roll"] = EnchantmentCategory.Recursos,
            ["ench.mercader"] = EnchantmentCategory.Recursos,
            ["ench.solitario"] = EnchantmentCategory.Recursos,

            // ⚔️ Ataque — daño o multiplicador a partir de una condición.
            ["ench.ancla"] = EnchantmentCategory.Ataque,
            ["ench.parity_gamble"] = EnchantmentCategory.Ataque,
            ["ench.pesado"] = EnchantmentCategory.Ataque,
            ["ench.resonante"] = EnchantmentCategory.Ataque,
            ["ench.volatil"] = EnchantmentCategory.Ataque,
            ["ench.enfiestado"] = EnchantmentCategory.Ataque,
            ["ench.racha"] = EnchantmentCategory.Ataque,
            ["ench.ejecutor"] = EnchantmentCategory.Ataque,

            // 🎛️ Control — restringen caras, modifican valores o alteran combos.
            ["ench.afilado"] = EnchantmentCategory.Control,
            ["ench.caras_centrales"] = EnchantmentCategory.Control,
            ["ench.cargado"] = EnchantmentCategory.Control,
            ["ench.comodin"] = EnchantmentCategory.Control,
            ["ench.escalador"] = EnchantmentCategory.Control,
            ["ench.extremos"] = EnchantmentCategory.Control,
            ["ench.gemelo"] = EnchantmentCategory.Control,
            ["ench.impar"] = EnchantmentCategory.Control,
            ["ench.invertido"] = EnchantmentCategory.Control,
            ["ench.mimetico"] = EnchantmentCategory.Control,
            ["ench.mitad_inferior"] = EnchantmentCategory.Control,
            ["ench.mitad_superior"] = EnchantmentCategory.Control,
            ["ench.multiplo_de_3"] = EnchantmentCategory.Control,
            ["ench.no_primo"] = EnchantmentCategory.Control,
            ["ench.only_evens"] = EnchantmentCategory.Control,
            ["ench.par"] = EnchantmentCategory.Control,
            ["ench.primo"] = EnchantmentCategory.Control,

            // 🗺️ Movimiento — todavía sin assets (los 7 del GDD apuntan al dado de
            // movimiento, que no existe como target de encantamiento en el código).
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

                if (!ByUpgradeId.TryGetValue(ench.UpgradeId ?? string.Empty, out var category))
                {
                    Debug.LogWarning($"[EnchCategories] '{ench.UpgradeId}' ({path}) sin entrada en el " +
                                     "diccionario — queda como está. Sumarlo y reejecutar.", ench);
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
