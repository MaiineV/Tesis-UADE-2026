using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.TMPSpriteAtlas
{
    /// <summary>
    /// Empaqueta <c>Art/UI/Frame/DmgIndicator.png</c> como atlas TMP de un solo glifo
    /// (<c>DmgIndicator</c>) y lo suma como fallback del default sprite asset, para que
    /// cualquier texto de tooltip pueda escribir el indicador de daño inline
    /// (<c>IconSpriteTags.DamageAmount</c>). Idempotente: re-correrlo re-genera el atlas
    /// y no duplica el fallback.
    /// </summary>
    public static class DamageIndicatorSpriteInstaller
    {
        private const string SourcePng = "Assets/Art/UI/Frame/DmgIndicator.png";
        private const string OutputFolder = "Assets/Art/UI/Icons";
        private const string AtlasName = "TMP_DmgIndicator";
        private const string GlyphName = "DmgIndicator";

        // El PNG a tamaño pleno de fuente se comía el renglón; 0.7 lo deja como
        // acompañante del número (pedido de playtest del 03/09).
        private const float GlyphScale = 0.7f;

        [MenuItem("Rollgeon/UI/Wire Damage Indicator TMP Sprite")]
        public static void Wire()
        {
            var sprite = AssetDatabase.LoadAllAssetsAtPath(SourcePng)
                .OfType<Sprite>()
                .FirstOrDefault();
            if (sprite == null)
            {
                Debug.LogError($"[DamageIndicatorSprite] No hay Sprite en '{SourcePng}' — " +
                               "revisá el import (Texture Type = Sprite).");
                return;
            }

            var inputs = new[] { new TMPSpriteAtlasBuilder.SpriteInput(sprite, GlyphName) };
            var result = TMPSpriteAtlasBuilder.Build(AtlasName, inputs, OutputFolder);
            if (!result.Success)
            {
                Debug.LogError($"[DamageIndicatorSprite] Falló el build del atlas: {result.Message}");
                return;
            }

            foreach (var character in result.Asset.spriteCharacterTable)
                character.scale = GlyphScale;
            result.Asset.UpdateLookupTables();
            EditorUtility.SetDirty(result.Asset);

            TMPSpriteAtlasBuilder.AddAsFallbackSpriteAsset(result.Asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DamageIndicatorSprite] Listo: <sprite name=\"{GlyphName}\"> resuelve " +
                      $"desde {OutputFolder}/{AtlasName}.asset (fallback del default).");
        }
    }
}
