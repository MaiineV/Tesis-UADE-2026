using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Filters;
using Rollgeon.Upgrades.Dice.Triggers;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Enchantment
{
    /// <summary>
    /// BUG-030b: re-autora <c>Ench_Afilado.asset</c> por código — <c>_faceFilter</c>
    /// pasa a <see cref="MinHalfMaxFilter"/> y <c>_triggers</c> se vacía (el bonus
    /// compensatorio sobra con la restricción real). Por tool y no editando el YAML:
    /// el stream de SerializationNodes de Odin renumera índices de tipo por orden de
    /// aparición y un edit manual lo desincroniza en silencio (comprobado: el asset
    /// editado a mano deserializaba <c>_faceFilter</c> como null). Acá setea los
    /// campos en memoria y deja que Odin re-serialice al guardar. Idempotente.
    /// </summary>
    public static class AfiladoFaceFilterFixTool
    {
        private const string AssetPath =
            "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Afilado.asset";

        [MenuItem("Rollgeon/Enchantments/Fix Afilado Face Filter")]
        public static void Fix()
        {
            var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(AssetPath);
            if (ench == null)
            {
                Debug.LogError($"[AfiladoFaceFilterFixTool] No se encontró {AssetPath}");
                return;
            }

            var so = typeof(EnchantmentSO);
            var filterField = so.GetField("_faceFilter", BindingFlags.NonPublic | BindingFlags.Instance);
            var triggersField = so.GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance);
            if (filterField == null || triggersField == null)
            {
                Debug.LogError("[AfiladoFaceFilterFixTool] Campos _faceFilter/_triggers no encontrados.");
                return;
            }

            bool dirty = false;
            if (filterField.GetValue(ench) is not MinHalfMaxFilter)
            {
                filterField.SetValue(ench, new MinHalfMaxFilter());
                dirty = true;
            }
            if (triggersField.GetValue(ench) is List<IEnchantmentTrigger> triggers && triggers.Count > 0)
            {
                triggers.Clear();
                dirty = true;
            }

            if (dirty)
            {
                EditorUtility.SetDirty(ench);
                AssetDatabase.SaveAssets();
                Debug.Log("[AfiladoFaceFilterFixTool] Ench_Afilado re-serializado con MinHalfMaxFilter y sin triggers.");
            }
            else
            {
                Debug.Log("[AfiladoFaceFilterFixTool] Ench_Afilado ya estaba correcto — no-op.");
            }
        }
    }
}
