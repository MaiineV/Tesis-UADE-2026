using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Helper de import para las texturas que los builders necesitan como <see cref="Sprite"/>
    /// (retratos de <c>BaseEntitySO.Portrait</c>, iconos de acción, etc.).
    /// </summary>
    /// <remarks>
    /// <b>Por qué hace falta.</b> Los símbolos de casino
    /// (<c>Assets/Art/2D/Symbols/Sprites/Casino_00XX.png</c>) están importados como
    /// <c>TextureImporterType.Default</c>, así que <c>LoadAssetAtPath&lt;Sprite&gt;</c> devuelve null y
    /// asignarlos a un <c>Portrait</c> desde código falla en silencio. Este helper flipea el importer a
    /// Sprite/Single y reimporta antes de devolverlos.
    /// </remarks>
    public static class SpriteImportUtility
    {
        /// <summary>
        /// Garantiza que <paramref name="texturePath"/> esté importada como Sprite (Single) y devuelve
        /// el <see cref="Sprite"/> resultante. <c>null</c> + warning si la textura no existe.
        /// </summary>
        /// <remarks>
        /// Si la textura ya está en modo <b>Multiple</b> se respeta el slicing y se devuelve el primer
        /// sub-sprite: forzarla a Single borraría los sub-sprites y dejaría en null todas las
        /// referencias que los usan (es exactamente el caso del atlas de la barra de vida).
        /// </remarks>
        public static Sprite EnsureSpriteImport(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                Debug.LogWarning("[SpriteImportUtility] texturePath vacío.");
                return null;
            }

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[SpriteImportUtility] No hay textura importable en " +
                                 $"'{texturePath}' — no se puede resolver un Sprite.");
                return null;
            }

            if (importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                Debug.LogWarning($"[SpriteImportUtility] '{texturePath}' está sliceada en modo " +
                                 $"Multiple: se deja como está y se devuelve el primer sub-sprite. " +
                                 $"Para un sprite puntual usá el sub-sprite por nombre.");
                return FirstSubSprite(texturePath);
            }

            if (ApplySpriteSettings(importer)) importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[SpriteImportUtility] '{texturePath}' se reimportó como Sprite pero " +
                                 $"no expone ninguno — ¿textura corrupta o de tamaño 0?");
            }
            return sprite;
        }

        /// <summary>
        /// <see cref="EnsureSpriteImport"/> en lote. Agrupa los reimports en un solo batch, que para
        /// las ~190 texturas de símbolos es la diferencia entre un reimport y 190.
        /// </summary>
        public static Dictionary<string, Sprite> EnsureSpriteImports(IEnumerable<string> texturePaths)
        {
            var result = new Dictionary<string, Sprite>();
            if (texturePaths == null) return result;

            var pending = new List<string>();
            var multiple = new List<string>();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in texturePaths)
                {
                    if (string.IsNullOrEmpty(path) || result.ContainsKey(path)) continue;

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                    {
                        Debug.LogWarning($"[SpriteImportUtility] No hay textura importable en '{path}'.");
                        result[path] = null;
                        continue;
                    }

                    if (importer.spriteImportMode == SpriteImportMode.Multiple)
                    {
                        multiple.Add(path);
                        continue;
                    }

                    if (ApplySpriteSettings(importer)) importer.SaveAndReimport();
                    pending.Add(path);
                }
            }
            finally
            {
                // Los reimports encolados recién se aplican acá: cargar los sprites antes daría null.
                AssetDatabase.StopAssetEditing();
            }

            foreach (var path in pending)
                result[path] = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            foreach (var path in multiple)
            {
                Debug.LogWarning($"[SpriteImportUtility] '{path}' está en modo Multiple — se respeta " +
                                 $"el slicing y se devuelve el primer sub-sprite.");
                result[path] = FirstSubSprite(path);
            }

            return result;
        }

        /// <summary>Sub-sprite por nombre de una textura sliceada en Multiple.</summary>
        public static Sprite FindSubSprite(string texturePath, string spriteName)
        {
            if (string.IsNullOrEmpty(texturePath) || string.IsNullOrEmpty(spriteName)) return null;

            foreach (var rep in AssetDatabase.LoadAllAssetRepresentationsAtPath(texturePath))
            {
                if (rep is Sprite sprite && sprite.name == spriteName) return sprite;
            }

            Debug.LogWarning($"[SpriteImportUtility] '{texturePath}' no tiene un sub-sprite " +
                             $"'{spriteName}'.");
            return null;
        }

        // ======================================================================
        // Internos
        // ======================================================================

        /// <summary>True si hubo que cambiar algo (y por lo tanto hay que reimportar).</summary>
        private static bool ApplySpriteSettings(TextureImporter importer)
        {
            bool dirty = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            // filterMode/compresión no se tocan: el pixel art del proyecto ya viene en Point y
            // pisarlo acá lo dejaría borroso.
            return dirty;
        }

        private static Sprite FirstSubSprite(string texturePath)
        {
            foreach (var rep in AssetDatabase.LoadAllAssetRepresentationsAtPath(texturePath))
            {
                if (rep is Sprite sprite) return sprite;
            }
            return null;
        }
    }
}
