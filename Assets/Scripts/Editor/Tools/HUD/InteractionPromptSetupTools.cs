using System.IO;
using System.Linq;
using Rollgeon.UI.HUD;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer del prompt inferior de interacción/compra: crea el settings SO
    /// en <c>Resources</c> (el <c>InteractionPromptView</c> es code-built y lo
    /// carga por <c>Resources.Load</c>) y le asigna el sprite de fondo del sheet
    /// de UI. Idempotente — reejecutar reasigna sin duplicar.
    /// </summary>
    public static class InteractionPromptSetupTools
    {
        private const string ResourcesFolder = "Assets/Resources/UI";
        private const string SettingsAssetPath = ResourcesFolder + "/InteractionPromptSettings.asset";
        private const string UiSheetPath = "Assets/Art/UI/UI-Sheet-sheet.png";
        private const string PanelSpriteName = "UI-Sheet-sheet_7";

        [MenuItem("Rollgeon/Interaction Prompt/Create Settings")]
        public static void CreateSettings()
        {
            if (!Directory.Exists(ResourcesFolder))
            {
                Directory.CreateDirectory(ResourcesFolder);
                AssetDatabase.Refresh();
            }

            var settings = AssetDatabase.LoadAssetAtPath<InteractionPromptSettingsSO>(SettingsAssetPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<InteractionPromptSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            }

            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(UiSheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == PanelSpriteName);
            if (sprite == null)
            {
                Debug.LogError($"[InteractionPromptSetup] Slice '{PanelSpriteName}' no encontrado en {UiSheetPath}. Abortando.");
                return;
            }

            settings.PanelSprite = sprite;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[InteractionPromptSetup] {SettingsAssetPath} listo con {PanelSpriteName}.");
        }
    }
}
