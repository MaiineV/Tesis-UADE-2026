using System.Collections.Generic;
using System.IO;
using Rollgeon.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Genera las siluetas placeholder de cada <see cref="DiceType"/> como PNG y autora el
    /// <see cref="DiceShapeCatalogSO"/> de Resources. Re-ejecutable: pisa los PNG y reescribe
    /// las entradas del catálogo sin duplicar.
    /// </summary>
    /// <remarks>
    /// Existe para desbloquear el código sin esperar al arte. El arte final se pisa encima de
    /// estos PNG (mismo path, mismo GUID) y el catálogo no se toca.
    /// </remarks>
    public static class DiceShapePlaceholderGenerator
    {
        private const string LogPrefix = "[DiceShapePlaceholders] ";
        private const string ShapesFolder = "Assets/Art/UI/Dice/Shapes";
        private const string CatalogPath = "Assets/Resources/Dice/DiceShapeCatalog.asset";
        private const int Size = 256;

        // Radio en espacio [-1,1]: deja un margen para que la AA del borde no se coma el píxel
        // del borde de la textura.
        private const float Radius = 0.94f;

        [MenuItem("Tools/Rollgeon/Dice/Generate Shape Placeholders")]
        public static void Run()
        {
            EnsureFolder("Assets/Art/UI", "Dice");
            EnsureFolder("Assets/Art/UI/Dice", "Shapes");
            EnsureFolder("Assets/Resources", "Dice");

            var sprites = new Dictionary<DiceType, Sprite>();

            try
            {
                var types = (DiceType[])System.Enum.GetValues(typeof(DiceType));
                for (int i = 0; i < types.Length; i++)
                {
                    var type = types[i];
                    EditorUtility.DisplayProgressBar("Dice shape placeholders",
                        $"Generando {type}…", (float)i / types.Length);

                    string path = $"{ShapesFolder}/Dice_Shape_{type}.png";
                    WritePng(path, BuildShape(type));
                    ImportAsSprite(path);

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        Debug.LogError($"{LogPrefix}No se pudo importar el sprite de {type} en {path}.");
                        return;
                    }
                    sprites[type] = sprite;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AuthorCatalog(sprites);
        }

        // -------------------------------------------------------------------
        // Formas — lenguaje de iconos estándar de dados
        // -------------------------------------------------------------------

        /// <summary>
        /// Vértices de la silueta en espacio [-1,1]. El círculo es un polígono de 64 lados para
        /// que todo comparta el mismo rasterizador.
        /// </summary>
        private static Vector2[] BuildShape(DiceType type) => type switch
        {
            DiceType.D3 => RegularPolygon(64, 90f),  // círculo
            DiceType.D4 => RegularPolygon(3, 90f),   // triángulo
            DiceType.D6 => RegularPolygon(4, 45f),   // cuadrado
            DiceType.D8 => RegularPolygon(4, 90f),   // rombo
            DiceType.D10 => Kite(),
            DiceType.D12 => RegularPolygon(5, 90f),  // pentágono
            DiceType.D20 => RegularPolygon(6, 90f),  // hexágono
            _ => RegularPolygon(4, 45f),
        };

        private static Vector2[] RegularPolygon(int sides, float startAngleDeg)
        {
            var verts = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.Deg2Rad * (startAngleDeg + 360f * i / sides);
                verts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
            }
            return verts;
        }

        /// <summary>Cara del d10: un deltoide, eje largo vertical.</summary>
        private static Vector2[] Kite() => new[]
        {
            new Vector2(0f, 1f) * Radius,
            new Vector2(0.62f, 0.18f) * Radius,
            new Vector2(0f, -1f) * Radius,
            new Vector2(-0.62f, 0.18f) * Radius,
        };

        // -------------------------------------------------------------------
        // Rasterizado
        // -------------------------------------------------------------------

        private static void WritePng(string path, Vector2[] poly)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];

            // Un píxel en espacio [-1,1]. La cobertura del borde se aproxima linealmente sobre
            // esa banda: es la aproximación estándar de AA y alcanza para un placeholder.
            float aa = 2f / Size;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(
                        (x + 0.5f) / Size * 2f - 1f,
                        (y + 0.5f) / Size * 2f - 1f);

                    float signed = SignedDistance(p, poly);
                    float coverage = Mathf.Clamp01(Mathf.InverseLerp(-aa, aa, signed));

                    // RGB blanco siempre: DiceSlotView tintea el Image (hold celeste, blocked
                    // gris) y un sprite blanco deja pasar el tint tal cual.
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(coverage * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Distancia con signo al polígono: positiva adentro, negativa afuera.</summary>
        private static float SignedDistance(Vector2 p, Vector2[] poly)
        {
            float minDist = float.MaxValue;
            bool inside = false;

            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                minDist = Mathf.Min(minDist, DistanceToSegment(p, poly[i], poly[j]));

                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }

            return inside ? minDist : -minDist;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lenSq = Vector2.Dot(ab, ab);
            if (lenSq < Mathf.Epsilon) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            return Vector2.Distance(p, a + t * ab);
        }

        // -------------------------------------------------------------------
        // Import + catálogo
        // -------------------------------------------------------------------

        private static void ImportAsSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void AuthorCatalog(Dictionary<DiceType, Sprite> sprites)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DiceShapeCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DiceShapeCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                Debug.Log($"{LogPrefix}Catálogo creado en {CatalogPath}.");
            }

            catalog.Shapes = new List<DiceShapeEntry>();
            foreach (DiceType type in System.Enum.GetValues(typeof(DiceType)))
                catalog.Shapes.Add(new DiceShapeEntry { Type = type, Shape = sprites[type] });

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!catalog.Validate(out var error))
            {
                Debug.LogError($"{LogPrefix}Catálogo inválido tras generar: {error}", catalog);
                return;
            }
            Debug.Log($"{LogPrefix}{catalog.Shapes.Count} formas generadas en {ShapesFolder} y catálogo validado.", catalog);
        }

        private static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}