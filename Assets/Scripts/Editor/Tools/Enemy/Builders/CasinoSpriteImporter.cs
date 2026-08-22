using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Convierte los PNG de <c>Assets/Art/2D/Symbols/Sprites</c> que usan los jefes nuevos como
    /// retrato (<c>BaseEntitySO.Portrait</c>) de textureType <b>Default</b> a <b>Sprite (Single)</b>
    /// (<c>Tools → Rollgeon → Bosses → Import Casino Sprites</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Una textura Default no la puede referenciar un campo <c>Sprite</c>: hasta que esto corra los
    /// builders de jefe no tienen retrato que asignar.
    /// </para>
    /// <para>
    /// Solo llama a <see cref="AssetImporter.SaveAndReimport"/> cuando algún campo difiere del
    /// target: reimportar por gusto cuesta segundos de editor y ensucia el diff de los <c>.meta</c>.
    /// </para>
    /// <para>
    /// El wrapMode NO se toca: varias de estas texturas también alimentan materiales de decal, y
    /// pasar de Repeat a Clamp les cambiaría el sampleo.
    /// </para>
    /// </remarks>
    public static class CasinoSpriteImporter
    {
        private const string LogPrefix = "[CasinoSpriteImporter] ";
        private const string SpriteFolder = "Assets/Art/2D/Symbols/Sprites";

        private const float TargetPixelsPerUnit = 100f;
        private const FilterMode TargetFilterMode = FilterMode.Point;

        /// <summary>
        /// Los símbolos que los jefes usan como retrato, más los que alimentan los materiales de
        /// decal temáticos.
        /// </summary>
        private static readonly string[] PortraitSpriteNames =
        {
            "Casino_0048", // ruleta      — Croupier / Decal_Ruleta
            "Casino_0050",
            "Casino_004D",
            "Casino_0064",
            "Casino_0070",
            "Casino_004E",
            "Casino_0044", // dados       — Decal_Dados
            "Casino_0046",
            "Casino_002A",
            "Casino_0054", // cartas      — Decal_Cartas
            "Casino_0052",
            "Casino_0049",
            "Casino_0051",
            "Casino_0038", // fichas      — Decal_Fichas
        };

        [MenuItem("Tools/Rollgeon/Bosses/Import Casino Sprites")]
        public static void ImportCasinoSprites()
        {
            var converted = new List<string>();
            int untouched = 0;
            int missing = 0;

            // Batch: sin esto cada SaveAndReimport dispara su propio refresh del AssetDatabase.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var spriteName in PortraitSpriteNames)
                {
                    string path = $"{SpriteFolder}/{spriteName}.png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        Debug.LogWarning(LogPrefix + $"No hay TextureImporter en '{path}' — salteado.");
                        missing++;
                        continue;
                    }

                    if (!ApplySpriteSettings(importer))
                    {
                        untouched++;
                        continue;
                    }

                    importer.SaveAndReimport();
                    converted.Add(spriteName);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            Debug.Log(LogPrefix + $"{converted.Count} convertido(s) a Sprite (Single), " +
                      $"{untouched} ya estaba(n) bien, {missing} faltante(s)." +
                      (converted.Count > 0 ? $" Convertidos: {string.Join(", ", converted)}." : string.Empty));
        }

        /// <summary>
        /// Escribe los settings de sprite en <paramref name="importer"/>.
        /// <c>false</c> si ya estaban todos — el caller se ahorra el reimport.
        /// </summary>
        private static bool ApplySpriteSettings(TextureImporter importer)
        {
            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }
            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, TargetPixelsPerUnit))
            {
                importer.spritePixelsPerUnit = TargetPixelsPerUnit;
                changed = true;
            }
            if (importer.filterMode != TargetFilterMode)
            {
                importer.filterMode = TargetFilterMode;
                changed = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }
            // Pixel art en UI: los mips solo aportan blur cuando el Image escala.
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            return changed;
        }
    }
}
