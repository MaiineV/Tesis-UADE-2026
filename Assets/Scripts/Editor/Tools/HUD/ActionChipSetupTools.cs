using System.Collections.Generic;
using Rollgeon.UI.HUD;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Cablea el look de los chips de acción (combate y exploración) al sheet
    /// <c>ChipWarrior</c>: base = <c>ChipWarrior_0</c>, hover/selected/deshabilitado =
    /// <c>ChipWarrior_1</c> (el alpha del deshabilitado lo maneja <see cref="ActionButton"/>).
    /// Idempotente — re-correr tras cambiar el arte del sheet.
    /// </summary>
    /// <remarks>
    /// Además fuerza <c>Transition.None</c> en el Button de cada chip: los estados
    /// visuales los posee <see cref="ActionButton"/> por completo, y un ColorTint de
    /// uGUI apilaba su gris de disabled sobre el alpha propio del chip.
    /// </remarks>
    public static class ActionChipSetupTools
    {
        private const string LogPrefix = "[ActionChipSetup] ";
        private const string SheetPath = "Assets/Art/UI/Chips/ChipAccions.png";

        private const string CombatHudPath = "Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab";
        private const string ExplorationHudPath = "Assets/Prefabs/UI/Canvas/Canvas_ExplorationHUD.prefab";

        [MenuItem("Rollgeon/Action Chips/Apply Action Chip Sprites")]
        public static void Apply()
        {
            var baseSprite = LoadSprite("ChipAccions_0");
            var highlightSprite = LoadSprite("ChipAccions_1");
            var usedSprite = LoadSprite("ChipAccions_2");
            if (baseSprite == null || highlightSprite == null || usedSprite == null)
            {
                Debug.LogError(LogPrefix + $"Faltan slices ChipAccions_0/1/2 en {SheetPath} — abortando.");
                return;
            }

            WireCombatChips(baseSprite, highlightSprite, usedSprite);
            WireExplorationChips(baseSprite, highlightSprite, usedSprite);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Combate: los chips ya tienen <see cref="ActionButton"/> — solo se
        /// cablean sprites y se normaliza la transition.</summary>
        private static void WireCombatChips(Sprite baseSprite, Sprite highlightSprite, Sprite usedSprite)
        {
            var root = PrefabUtility.LoadPrefabContents(CombatHudPath);
            try
            {
                int wired = 0;
                foreach (var chip in root.GetComponentsInChildren<ActionButton>(true))
                {
                    var so = new SerializedObject(chip);
                    so.FindProperty("_highlightSprite").objectReferenceValue = highlightSprite;
                    so.FindProperty("_usedSprite").objectReferenceValue = usedSprite;
                    var button = so.FindProperty("_button").objectReferenceValue as Button;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    if (button == null) button = chip.GetComponent<Button>();
                    WireButton(button, baseSprite);
                    wired++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, CombatHudPath);
                Debug.Log(LogPrefix + $"{CombatHudPath}: {wired} chips cableados (ActionButton).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Exploración: los chips son Buttons planos de
        /// <see cref="ExplorationActionButtonsView"/> — se les agrega (o actualiza)
        /// un <see cref="ChipButtonVisual"/> con el mismo contrato de estados.</summary>
        private static void WireExplorationChips(Sprite baseSprite, Sprite highlightSprite, Sprite usedSprite)
        {
            var root = PrefabUtility.LoadPrefabContents(ExplorationHudPath);
            try
            {
                var view = root.GetComponentInChildren<ExplorationActionButtonsView>(true);
                if (view == null)
                {
                    Debug.LogWarning(LogPrefix + $"{ExplorationHudPath}: sin ExplorationActionButtonsView — nada que cablear.");
                    return;
                }

                var viewSo = new SerializedObject(view);
                var buttonsProp = viewSo.FindProperty("_buttons");
                int wired = 0;
                for (int i = 0; i < buttonsProp.arraySize; i++)
                {
                    var button = buttonsProp.GetArrayElementAtIndex(i).objectReferenceValue as Button;
                    if (button == null) continue;

                    var visual = button.GetComponent<ChipButtonVisual>();
                    if (visual == null) visual = button.gameObject.AddComponent<ChipButtonVisual>();

                    var so = new SerializedObject(visual);
                    so.FindProperty("_highlightSprite").objectReferenceValue = highlightSprite;
                    so.FindProperty("_usedSprite").objectReferenceValue = usedSprite;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    WireButton(button, baseSprite);
                    wired++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, ExplorationHudPath);
                Debug.Log(LogPrefix + $"{ExplorationHudPath}: {wired} chips cableados (ChipButtonVisual).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireButton(Button button, Sprite baseSprite)
        {
            if (button == null) return;
            if (button.targetGraphic is Image image) image.sprite = baseSprite;
            // Los estados visuales los posee el componente de chip — el tint/swap
            // de uGUI apilaba su gris de disabled sobre el alpha propio.
            button.transition = Selectable.Transition.None;
        }

        private static Sprite LoadSprite(string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
            }
            return null;
        }
    }
}
