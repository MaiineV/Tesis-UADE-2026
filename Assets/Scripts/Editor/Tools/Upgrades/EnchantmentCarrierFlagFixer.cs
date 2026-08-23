using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;

namespace Rollgeon.EditorTools.Upgrades
{
    /// <summary>
    /// Fix del bug "encantamientos condicionales afectan la tirada entera aunque el
    /// dado no participe" (diagnóstico: <see cref="ExecuteEffectsOnDiceEvent.RequireCarrierParticipates"/>
    /// quedó en <c>false</c> en 4 assets que SÍ dependen de la participación del carrier
    /// — Resonante, Gemelo, Fragil y ParityGamble usan <see cref="PreConditions.PcCarrierFace"/>
    /// con <c>Mode = HasDuplicate</c>, que solo tiene sentido evaluado sobre los dados que
    /// realmente formaron el combo). Re-autora por código y no a mano: los 4 assets son
    /// <c>SerializedScriptableObject</c> — el YAML mezcla el stream de
    /// <c>SerializationData.SerializationNodes</c> con el bloque <c>references</c>
    /// (RefIds); un edit manual del int desincroniza uno de los dos y Odin deserializa
    /// silenciosamente distinto de lo que el archivo "dice" a simple vista. Acá se muta
    /// la instancia real del trigger en memoria y se deja que Odin re-serialice ambos
    /// bloques al guardar. Idempotente — correrlo de nuevo sobre assets ya arreglados
    /// es un no-op.
    /// </summary>
    public static class EnchantmentCarrierFlagFixer
    {
        private static readonly string[] AssetPaths =
        {
            "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Resonante.asset",
            "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Gemelo.asset",
            "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_Fragil.asset",
            "Assets/Rollgeon/Upgrades/Dice/Enchantments/Ench_ParityGamble.asset",
        };

        [MenuItem("Rollgeon/Upgrades/Fix Carrier Participation Flags")]
        public static void Fix()
        {
            int fixedCount = 0;
            int alreadyOkCount = 0;
            var missing = new List<string>();

            foreach (var path in AssetPaths)
            {
                var ench = AssetDatabase.LoadAssetAtPath<EnchantmentSO>(path);
                if (ench == null)
                {
                    missing.Add(path);
                    continue;
                }

                bool dirty = false;
                bool touchedAny = false;
                foreach (var trigger in ench.Triggers)
                {
                    if (trigger is not ExecuteEffectsOnDiceEvent bridge) continue;
                    if (bridge.Event != EnchantmentHookEvent.ComboMatched
                        && bridge.Event != EnchantmentHookEvent.ComboPlayed) continue;

                    touchedAny = true;
                    if (bridge.RequireCarrierParticipates) continue;

                    bridge.RequireCarrierParticipates = true;
                    dirty = true;
                }

                if (!touchedAny)
                {
                    Debug.LogWarning($"[EnchantmentCarrierFlagFixer] {path}: no se encontró un " +
                                      "ExecuteEffectsOnDiceEvent en ComboMatched/ComboPlayed — nada que tocar.");
                    continue;
                }

                if (dirty)
                {
                    EditorUtility.SetDirty(ench);
                    fixedCount++;
                    Debug.Log($"[EnchantmentCarrierFlagFixer] {path}: RequireCarrierParticipates → true.");
                }
                else
                {
                    alreadyOkCount++;
                }
            }

            if (fixedCount > 0) AssetDatabase.SaveAssets();

            foreach (var path in missing)
                Debug.LogError($"[EnchantmentCarrierFlagFixer] No se encontró {path}");

            Debug.Log($"[EnchantmentCarrierFlagFixer] Listo — {fixedCount} asset(s) corregidos, " +
                      $"{alreadyOkCount} ya estaban OK, {missing.Count} no encontrados.");
        }
    }
}
