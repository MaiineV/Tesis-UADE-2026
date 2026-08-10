using System;
using System.Text;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Tooltips;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer de los chips de acción del HUD de combate. Cablea el <c>_costLabel</c>
    /// de cada <see cref="ActionButton"/> a su hijo <c>Cost</c> — venían en
    /// <c>{fileID: 0}</c> con el número hardcodeado en el TMP, así que el código nunca
    /// podía tocarlo (ni para escribir el costo real, ni para pintarlo de rojo cuando
    /// no alcanza la energía). Idempotente: reejecutar no duplica ni pisa de más.
    /// </summary>
    public static class ActionChipSetupTools
    {
        private const string CombatHudPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab";
        private const string CostChildName = "Cost";

        [MenuItem("Rollgeon/Action Chips/Wire Cost Labels")]
        public static void WireCostLabels()
        {
            var root = PrefabUtility.LoadPrefabContents(CombatHudPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[ActionChipSetupTools] No se pudo abrir {CombatHudPrefabPath}.");
                return;
            }

            int wired = 0, alreadyWired = 0, missing = 0;
            var report = new StringBuilder();

            try
            {
                var buttons = root.GetComponentsInChildren<ActionButton>(includeInactive: true);
                if (buttons.Length == 0)
                {
                    Debug.LogWarning("[ActionChipSetupTools] No se encontró ningún ActionButton en el prefab.");
                    return;
                }

                foreach (var button in buttons)
                {
                    var so = new SerializedObject(button);
                    var prop = so.FindProperty("_costLabel");
                    if (prop == null)
                    {
                        Debug.LogWarning($"[ActionChipSetupTools] {button.name}: el campo _costLabel ya no existe.");
                        continue;
                    }

                    var costTransform = button.transform.Find(CostChildName);
                    var label = costTransform != null
                        ? costTransform.GetComponent<TextMeshProUGUI>()
                        : null;

                    if (label == null)
                    {
                        missing++;
                        Debug.LogWarning(
                            $"[ActionChipSetupTools] {button.name}: no hay hijo '{CostChildName}' con TextMeshProUGUI.",
                            button);
                        continue;
                    }

                    if (prop.objectReferenceValue == label) alreadyWired++;
                    else
                    {
                        prop.objectReferenceValue = label;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        wired++;
                    }

                    report.AppendLine(DescribeCost(button, label));
                }

                PrefabUtility.SaveAsPrefabAsset(root, CombatHudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ActionChipSetupTools] Cost labels: {wired} cableados, {alreadyWired} ya estaban, " +
                      $"{missing} sin hijo '{CostChildName}'.\n{report}");
        }

        /// <summary>
        /// Compara el texto hardcodeado del prefab contra el costo resuelto. No lo pisa:
        /// solo reporta, para detectar que el arte y el dato se separaron al agregar
        /// clases nuevas.
        /// </summary>
        /// <remarks>
        /// La comparación es orientativa, no autoritativa: las acciones con costo
        /// contextual (Curarse y Forzar Puerta cuestan distinto dentro y fuera de
        /// combate) resuelven fuera de play mode como si estuvieran en exploración.
        /// Un aviso en esas dos filas es esperable y no significa nada.
        /// </remarks>
        private static string DescribeCost(ActionButton button, TMP_Text label)
        {
            string authored = label.text;
            if (!TryResolveRuntimeCost(button.Slot, out int runtimeCost))
                return $"  · {button.name}: prefab='{authored}' (sin hero de referencia para comparar)";

            string suffix = authored.Trim() == runtimeCost.ToString()
                ? "coincide"
                : $"difiere → fuera de play mode resuelve '{runtimeCost}' " +
                  "(esperable si el costo depende de estar en combate)";
            return $"  · {button.name}: prefab='{authored}' — {suffix}";
        }

        // Usa el primer ClassHeroSO que encuentre como referencia. No es autoritativo
        // (cada clase tiene sus costos), pero alcanza para detectar que el número
        // dibujado dejó de tener relación con el dato.
        private static bool TryResolveRuntimeCost(HeroBehaviorSlot slot, out int cost)
        {
            cost = 0;
            var guids = AssetDatabase.FindAssets("t:ClassHeroSO");
            if (guids.Length == 0) return false;

            var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            var behavior = hero != null ? hero.ResolveBaseBehavior(slot, GamePhase.Combat) : null;
            if (behavior == null) return false;

            cost = HeroActionTooltip.ResolveDisplayCost(behavior, Guid.Empty);
            return true;
        }
    }
}
