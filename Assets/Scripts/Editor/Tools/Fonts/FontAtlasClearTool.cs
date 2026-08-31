using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// BUG-076: el glifo de la "i" de <c>m6x11plus.ttf</c> se reeditó (gap punto/asta
    /// agrandado de 1px a 2px de grilla) pero <c>m6x11plus SDF.asset</c> es un font asset
    /// Dynamic con los glifos ya rasterizados CACHEADOS en el asset serializado — sin
    /// limpiar ese cache, TMP sigue mostrando la "i" vieja.
    ///
    /// <c>Tools → Rollgeon → Fonts → Clear m6x11plus Dynamic Atlas (BUG-076)</c> vacía el
    /// cache; TMP re-rasteriza cada glifo desde el TTF la próxima vez que se usa.
    /// Idempotente e inocuo: correrlo de más solo fuerza re-rasterizaciones.
    /// </summary>
    public static class FontAtlasClearTool
    {
        private const string LogPrefix = "[FontAtlasClearTool] ";
        private const string FontAssetPath = "Assets/Fonts/m6x11plus SDF.asset";

        [MenuItem("Tools/Rollgeon/Fonts/Clear m6x11plus Dynamic Atlas (BUG-076)")]
        public static void ClearM6x11PlusAtlas()
        {
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                Debug.LogError(LogPrefix + $"No se encontró '{FontAssetPath}'.");
                return;
            }

            fontAsset.ClearFontAssetData(setAtlasSizeToZero: true);
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();

            Debug.Log(LogPrefix + "Atlas dinámico limpiado — TMP re-rasteriza desde el TTF " +
                      "editado. Verificar visualmente que la 'i' se distinga de la 'l'.");
        }
    }
}
