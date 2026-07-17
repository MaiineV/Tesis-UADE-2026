using System.IO;
using System.Linq;
using Rollgeon.UI.Cursor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Cursor
{
    /// <summary>
    /// Crea el asset de settings del cursor custom en Resources y le asigna los
    /// 4 sprites del sheet. Idempotente — reejecutar reasigna sin duplicar.
    /// </summary>
    public static class CursorSetupTools
    {
        private const string SheetPath = "Assets/Art/UI/Pointer/Pointer-Sheet.png";
        private const string ResourcesDir = "Assets/Resources/Cursor";
        private const string SettingsPath = "Assets/Resources/Cursor/CursorSettings.asset";

        [MenuItem("Rollgeon/Cursor/Setup")]
        public static void Setup()
        {
            // Los 4 slices del sheet, ordenados por nombre (Pointer-Sheet_0.._3).
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (sprites.Length < 4)
            {
                Debug.LogError($"[CursorSetup] El sheet {SheetPath} tiene {sprites.Length} sprites; " +
                               "se esperaban 4 (Sprite Mode = Multiple, sliced en 4).");
                return;
            }

            Directory.CreateDirectory(ResourcesDir);

            var settings = AssetDatabase.LoadAssetAtPath<CursorSettingsSO>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CursorSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            settings.StateSprites = new[] { sprites[0], sprites[1], sprites[2], sprites[3] };
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log("[CursorSetup] CursorSettings listo con los 4 sprites del cursor.");
        }
    }
}
