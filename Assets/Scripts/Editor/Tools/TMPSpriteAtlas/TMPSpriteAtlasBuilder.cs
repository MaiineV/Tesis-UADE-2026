using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Rollgeon.UI.Utility;
using TMPro;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.TextCore;

namespace Rollgeon.EditorTools.TMPSpriteAtlas
{
    /// <summary>
    /// Motor del generador de atlas: empaquetado, creación del <see cref="TMP_SpriteAsset"/>
    /// y cableado a TMP Settings / <see cref="IconPlaceholderMapSO"/>. Sin GUI, para que la
    /// generación se pueda scriptear (setup tools, MCP) además de correrla a mano desde
    /// <see cref="TMPSpriteAtlasGeneratorWindow"/>.
    /// </summary>
    public static class TMPSpriteAtlasBuilder
    {
        /// <summary>Un sprite fuente + el nombre con el que entra al atlas.</summary>
        public readonly struct SpriteInput
        {
            public readonly Sprite Sprite;
            public readonly string Name;

            public SpriteInput(Sprite sprite, string name)
            {
                Sprite = sprite;
                Name = name;
            }
        }

        public readonly struct BuildResult
        {
            public readonly bool Success;
            public readonly string Message;
            public readonly TMP_SpriteAsset Asset;

            public BuildResult(bool success, string message, TMP_SpriteAsset asset)
            {
                Success = success;
                Message = message;
                Asset = asset;
            }
        }

        // "Energy_0" -> "Energy". El sufijo lo agrega Unity al cortar un PNG en modo
        // Multiple con un solo frame; no aporta nada al nombre del glifo.
        private static readonly Regex TrailingIndex = new(@"_\d+$", RegexOptions.Compiled);

        /// <summary>Nombre de glifo sugerido para un sprite recién arrastrado.</summary>
        public static string DefaultGlyphName(Sprite sprite)
            => sprite == null ? "" : TrailingIndex.Replace(sprite.name, "");

        /// <summary>
        /// Motivo por el que <paramref name="inputs"/> no se puede empaquetar, o <c>null</c>.
        /// Nombres vacíos o repetidos romperían el lookup de TMP en silencio (el segundo
        /// glifo con el mismo nombre nunca se resuelve), así que se bloquean antes.
        /// </summary>
        public static string Validate(IReadOnlyList<SpriteInput> inputs)
        {
            if (inputs == null || inputs.Count == 0)
                return "No hay sprites para empaquetar.";

            if (inputs.Any(e => string.IsNullOrWhiteSpace(e.Name)))
                return "Hay sprites sin nombre de glifo.";

            var dupes = inputs
                .GroupBy(e => e.Name.Trim())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            return dupes.Count > 0
                ? $"Nombres de glifo repetidos: {string.Join(", ", dupes)}"
                : null;
        }

        /// <summary>
        /// Empaqueta <paramref name="inputs"/> en <c>{outputFolder}/{atlasName}.png</c> y crea
        /// el <see cref="TMP_SpriteAsset"/> hermano. Sobrescribe si ya existían.
        /// </summary>
        public static BuildResult Build(
            string atlasName,
            IReadOnlyList<SpriteInput> inputs,
            string outputFolder,
            int padding = 2,
            int maxAtlasSize = 512)
        {
            string invalid = Validate(inputs);
            if (invalid != null) return new BuildResult(false, invalid, null);

            if (string.IsNullOrWhiteSpace(atlasName))
                return new BuildResult(false, "Falta el nombre del atlas.", null);

            if (!AssetDatabase.IsValidFolder(outputFolder))
                return new BuildResult(false, $"Output folder '{outputFolder}' does not exist.", null);

            var originalReadableStates = new Dictionary<string, bool>();

            try
            {
                var spriteNames = inputs.Select(e => e.Name.Trim()).ToArray();

                // Paso 1: los sprites fuente casi nunca son readable — se prende el flag,
                // se extraen los píxeles y al final se restaura como estaba.
                var spriteTextures = new Texture2D[inputs.Count];
                for (int i = 0; i < inputs.Count; i++)
                {
                    var sprite = inputs[i].Sprite;
                    if (sprite == null)
                        return new BuildResult(false, $"El sprite de '{spriteNames[i]}' es null.", null);

                    string texPath = AssetDatabase.GetAssetPath(sprite.texture);
                    if (AssetImporter.GetAtPath(texPath) is TextureImporter importer && !importer.isReadable)
                    {
                        originalReadableStates.TryAdd(texPath, false);
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }

                    var spriteRect = sprite.rect;
                    var extracted = new Texture2D((int)spriteRect.width, (int)spriteRect.height, TextureFormat.RGBA32, false);
                    extracted.SetPixels(sprite.texture.GetPixels(
                        (int)spriteRect.x, (int)spriteRect.y,
                        (int)spriteRect.width, (int)spriteRect.height));
                    extracted.Apply();
                    spriteTextures[i] = extracted;
                }

                // Paso 2: packear. PackTextures downscalea solo si no entra en maxAtlasSize.
                var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var uvRects = atlas.PackTextures(spriteTextures, padding, maxAtlasSize);

                if (uvRects == null)
                {
                    UnityEngine.Object.DestroyImmediate(atlas);
                    return new BuildResult(false, "Failed to pack textures. Try increasing the max atlas size.", null);
                }

                int atlasWidth = atlas.width;
                int atlasHeight = atlas.height;

                string pngPath = $"{outputFolder}/{atlasName}.png";
                File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), pngPath), atlas.EncodeToPNG());

                foreach (var t in spriteTextures) UnityEngine.Object.DestroyImmediate(t);
                UnityEngine.Object.DestroyImmediate(atlas);

                AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

                // Paso 3: el PNG entra como Sprite/Multiple con un sub-sprite por glifo.
                if (AssetImporter.GetAtPath(pngPath) is TextureImporter atlasImporter)
                {
                    atlasImporter.textureType = TextureImporterType.Sprite;
                    atlasImporter.spriteImportMode = SpriteImportMode.Multiple;
                    atlasImporter.isReadable = false;
                    atlasImporter.filterMode = FilterMode.Bilinear;
                    atlasImporter.maxTextureSize = maxAtlasSize;
                    atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    atlasImporter.alphaIsTransparency = true;
                    atlasImporter.SaveAndReimport();

                    // Los sub-sprites se autoran por el ISpriteEditorDataProvider: en Unity 6
                    // TextureImporter.spritesheet quedó como no-op y setearlo no crea nada.
                    // Mismo idiom que ClassSelectionSetupTools / EnchantmentAltarSetupTools.
                    var factory = new SpriteDataProviderFactories();
                    factory.Init();
                    var provider = factory.GetSpriteEditorDataProviderFromObject(atlasImporter);
                    provider.InitSpriteEditorDataProvider();

                    var rects = new SpriteRect[inputs.Count];
                    for (int i = 0; i < inputs.Count; i++)
                    {
                        rects[i] = new SpriteRect
                        {
                            name = spriteNames[i],
                            spriteID = GUID.Generate(),
                            rect = PixelRect(uvRects[i], atlasWidth, atlasHeight),
                            alignment = SpriteAlignment.Center,
                            pivot = new Vector2(0.5f, 0.5f)
                        };
                    }

                    provider.SetSpriteRects(rects);
                    provider.Apply();
                    EditorUtility.SetDirty(atlasImporter);
                    atlasImporter.SaveAndReimport();
                }

                // Paso 4: el TMP_SpriteAsset con su glyph/character table.
                string assetPath = $"{outputFolder}/{atlasName}.asset";
                AssetDatabase.DeleteAsset(assetPath);

                var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(spriteAsset, assetPath);

                var atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
                spriteAsset.spriteSheet = atlasTexture;
                spriteAsset.hashCode = TMP_TextUtilities.GetSimpleHashCode(atlasName);

                var atlasSprites = AssetDatabase.LoadAllAssetsAtPath(pngPath).OfType<Sprite>().ToArray();

                var glyphTable = spriteAsset.spriteGlyphTable;
                var charTable = spriteAsset.spriteCharacterTable;

                for (int i = 0; i < inputs.Count; i++)
                {
                    var r = PixelRect(uvRects[i], atlasWidth, atlasHeight);
                    var subSprite = atlasSprites.FirstOrDefault(s => s.name == spriteNames[i]);

                    // bearingY = h * 0.9 deja el icono apoyado casi en la baseline, que es
                    // como lee bien al lado de texto en mayúsculas.
                    var glyph = new TMP_SpriteGlyph(
                        (uint)i,
                        new GlyphMetrics(r.width, r.height, 0, r.height * 0.9f, r.width),
                        new GlyphRect((int)r.x, (int)r.y, (int)r.width, (int)r.height),
                        1.0f,
                        0,
                        subSprite);
                    glyphTable.Add(glyph);

                    charTable.Add(new TMP_SpriteCharacter(0xFFFE, glyph)
                    {
                        name = spriteNames[i],
                        scale = 1.0f
                    });
                }

                // El setter de m_Version es internal — TMP lo necesita seteado para no
                // intentar migrar el asset desde un formato viejo al cargarlo.
                var so = new SerializedObject(spriteAsset);
                so.FindProperty("m_Version").stringValue = "1.1.0";
                so.ApplyModifiedPropertiesWithoutUndo();

                var material = new Material(Shader.Find("TextMeshPro/Sprite")) { name = atlasName + " Material" };
                material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
                spriteAsset.material = material;
                AssetDatabase.AddObjectToAsset(material, spriteAsset);

                spriteAsset.UpdateLookupTables();

                EditorUtility.SetDirty(spriteAsset);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return new BuildResult(true,
                    $"Atlas generado.\nTexture: {pngPath} ({atlasWidth}x{atlasHeight})\n" +
                    $"Asset: {assetPath} ({inputs.Count} sprites: {string.Join(", ", spriteNames)})",
                    spriteAsset);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new BuildResult(false, $"Error: {e.Message}", null);
            }
            finally
            {
                RestoreReadableStates(originalReadableStates);
            }
        }

        /// <summary>
        /// Deja <paramref name="spriteAsset"/> como default sprite asset del proyecto.
        /// Con <paramref name="demoteCurrentToFallback"/> el default anterior no se pierde:
        /// pasa a ser fallback del nuevo (si no, sus glifos dejarían de resolverse).
        /// </summary>
        public static void SetAsDefaultSpriteAsset(TMP_SpriteAsset spriteAsset, bool demoteCurrentToFallback)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogError("[TMP Sprite Atlas] TMP Settings no encontrado.");
                return;
            }

            var currentDefault = TMP_Settings.defaultSpriteAsset;
            if (demoteCurrentToFallback && currentDefault != null && currentDefault != spriteAsset)
                AddFallbackTo(spriteAsset, currentDefault);

            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = spriteAsset;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TMP Sprite Atlas] '{spriteAsset.name}' es ahora el default sprite asset.");
        }

        /// <summary>Suma <paramref name="spriteAsset"/> a los fallbacks del default actual.</summary>
        public static void AddAsFallbackSpriteAsset(TMP_SpriteAsset spriteAsset)
        {
            var defaultAsset = TMP_Settings.defaultSpriteAsset;
            if (defaultAsset == null)
            {
                Debug.LogError("[TMP Sprite Atlas] No hay default sprite asset en TMP Settings.");
                return;
            }

            if (defaultAsset == spriteAsset)
            {
                Debug.Log($"[TMP Sprite Atlas] '{spriteAsset.name}' ya es el default sprite asset.");
                return;
            }

            AddFallbackTo(defaultAsset, spriteAsset);
        }

        private static void AddFallbackTo(TMP_SpriteAsset host, TMP_SpriteAsset fallback)
        {
            var so = new SerializedObject(host);
            var prop = so.FindProperty("fallbackSpriteAssets");

            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == fallback)
                {
                    Debug.Log($"[TMP Sprite Atlas] '{fallback.name}' ya es fallback de '{host.name}'.");
                    return;
                }
            }

            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = fallback;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(host);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TMP Sprite Atlas] '{fallback.name}' agregado como fallback de '{host.name}'.");
        }

        /// <summary>
        /// Repuebla <paramref name="placeholderMap"/> desde el default sprite asset y sus
        /// fallbacks. Devuelve cuántos mappings quedaron.
        /// </summary>
        public static int RebuildMappingsFromSettings(IconPlaceholderMapSO placeholderMap)
        {
            if (placeholderMap == null) return 0;

            var so = new SerializedObject(placeholderMap);
            var mappingsProp = so.FindProperty("mappings");
            mappingsProp.ClearArray();

            var seen = new HashSet<string>();
            int total = 0;

            var defaultAsset = TMP_Settings.defaultSpriteAsset;
            if (defaultAsset != null)
            {
                total += AddMappingsFromAsset(defaultAsset, mappingsProp, seen);

                if (defaultAsset.fallbackSpriteAssets != null)
                {
                    foreach (var fallback in defaultAsset.fallbackSpriteAssets)
                    {
                        if (fallback != null)
                            total += AddMappingsFromAsset(fallback, mappingsProp, seen);
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(placeholderMap);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TMP Sprite Atlas] {total} mappings reconstruidos en '{placeholderMap.name}'.");
            return total;
        }

        private static int AddMappingsFromAsset(TMP_SpriteAsset spriteAsset, SerializedProperty mappingsProp, HashSet<string> seen)
        {
            int added = 0;
            foreach (var character in spriteAsset.spriteCharacterTable)
            {
                string spriteName = character.name;
                if (string.IsNullOrEmpty(spriteName)) continue;

                string placeholder = ConvertToPlaceholder(spriteName);
                if (!seen.Add(placeholder)) continue;

                int index = mappingsProp.arraySize;
                mappingsProp.InsertArrayElementAtIndex(index);
                var element = mappingsProp.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("placeholder").stringValue = placeholder;
                element.FindPropertyRelative("spriteName").stringValue = spriteName;
                added++;
            }
            return added;
        }

        /// <summary>"DamageIcon" → "DAMAGE_ICON", "Energy" → "ENERGY".</summary>
        public static string ConvertToPlaceholder(string spriteName)
            => Regex.Replace(spriteName, "(?<!^)([A-Z])", "_$1").ToUpperInvariant();

        private static Rect PixelRect(Rect uv, int atlasWidth, int atlasHeight) => new(
            Mathf.RoundToInt(uv.x * atlasWidth),
            Mathf.RoundToInt(uv.y * atlasHeight),
            Mathf.RoundToInt(uv.width * atlasWidth),
            Mathf.RoundToInt(uv.height * atlasHeight));

        private static void RestoreReadableStates(Dictionary<string, bool> states)
        {
            foreach (var kvp in states)
            {
                if (AssetImporter.GetAtPath(kvp.Key) is TextureImporter importer)
                {
                    importer.isReadable = kvp.Value;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
