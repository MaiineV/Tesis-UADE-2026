using System.IO;
using System.Linq;
using Rollgeon.UI.Cursor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Cursor
{
    /// <summary>
    /// Bakea los 4 slices del sheet del cursor a texturas standalone (import
    /// type Cursor, escaladas nearest-neighbor) y las asigna al settings en
    /// Resources. Idempotente — reejecutar regenera y reasigna sin duplicar.
    /// El cursor usa <c>Cursor.SetCursor</c> (hardware): necesita una textura
    /// entera por estado, no un sub-rect de atlas, y la escala se hornea acá
    /// porque el OS dibuja la textura a tamaño nativo.
    /// </summary>
    public static class CursorSetupTools
    {
        private const string SheetPath = "Assets/Art/UI/Pointer/Pointer-Sheet.png";
        private const string BakedDir = "Assets/Art/UI/Pointer/Baked";
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
            Directory.CreateDirectory(BakedDir);

            var settings = AssetDatabase.LoadAssetAtPath<CursorSettingsSO>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CursorSettingsSO>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            // El sheet se relee del PNG en disco: independiza el bake del flag
            // Read/Write del importer del atlas.
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            sheet.LoadImage(File.ReadAllBytes(SheetPath));

            int scale = Mathf.Max(1, Mathf.RoundToInt(settings.Scale));
            var baked = new Texture2D[4];
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    string path = $"{BakedDir}/Cursor_{i}.png";
                    BakeSlice(sheet, sprites[i].rect, scale, path);
                    ImportAsCursor(path);
                    baked[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }
            finally
            {
                Object.DestroyImmediate(sheet);
            }

            settings.StateCursors = baked;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CursorSetup] CursorSettings listo: 4 texturas de cursor bakeadas a {scale}x.");
        }

        private static void BakeSlice(Texture2D sheet, Rect rect, int scale, string outPath)
        {
            int x = (int)rect.x, y = (int)rect.y, w = (int)rect.width, h = (int)rect.height;
            var src = sheet.GetPixels(x, y, w, h);

            int ws = w * scale, hs = h * scale;
            var dst = new Color[ws * hs];
            for (int py = 0; py < hs; py++)
            {
                for (int px = 0; px < ws; px++)
                {
                    dst[py * ws + px] = src[(py / scale) * w + (px / scale)];
                }
            }

            var tex = new Texture2D(ws, hs, TextureFormat.RGBA32, mipChain: false);
            try
            {
                tex.SetPixels(dst);
                tex.Apply();
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        private static void ImportAsCursor(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Cursor;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
