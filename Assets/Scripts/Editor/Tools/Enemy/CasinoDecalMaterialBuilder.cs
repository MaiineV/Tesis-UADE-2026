using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Crea los materiales de decal temáticos de las salas de jefe clonando
    /// <c>DecalHerradura.mat</c> (<c>Tools → Rollgeon → Bosses → Build Casino Decal Materials</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué clonar y no crear.</b> El material de referencia ya tiene el shadergraph
    /// (<c>Assets/Shaders/DecalSymbols.shadergraph</c>), sus keywords y los floats de bias/draw order
    /// del decal de URP. Crear un <c>Material</c> nuevo por código deja esos valores en el default del
    /// shader y el decal se ve distinto sin que se note por qué; <c>CopyAsset</c> arranca de algo que
    /// ya funciona en escena y sólo cambia textura y color.
    /// </para>
    /// <para>
    /// <b>Idempotente.</b> Si el <c>.mat</c> existe, lo repopula en vez de recrearlo — así conserva su
    /// GUID y no rompe las salas que ya lo tengan cableado. Si textura y color ya coinciden, no marca
    /// el asset como sucio.
    /// </para>
    /// <para>
    /// No cablea ninguna sala: el wiring de los <c>DecalProjector</c> es trabajo de escena.
    /// </para>
    /// </remarks>
    public static class CasinoDecalMaterialBuilder
    {
        private const string LogPrefix = "[CasinoDecalMaterialBuilder] ";
        private const string SourceMaterialPath = "Assets/Art/2D/Symbols/DecalHerradura.mat";
        private const string SymbolFolder = "Assets/Art/2D/Symbols/Sprites";
        private const string OutputFolder = "Assets/Art/2D/Symbols";

        private const string BaseMapProperty = "Base_Map";
        private const string ColorProperty = "_Color";

        /// <summary>Un decal temático: nombre del <c>.mat</c>, símbolo y tinte.</summary>
        private readonly struct DecalSpec
        {
            public readonly string MaterialName;
            public readonly string TextureName;
            public readonly Color Color;

            public DecalSpec(string materialName, string textureName, Color color)
            {
                MaterialName = materialName;
                TextureName = textureName;
                Color = color;
            }
        }

        // Paleta de casino: cada jefe tiene su símbolo y su color. Los valores son sRGB directos
        // (el shadergraph tintea la textura), pensados para tocar acá y re-correr el menú.
        private static readonly DecalSpec[] Specs =
        {
            new DecalSpec("Decal_Ruleta", "Casino_0048", new Color(0.435f, 0.078f, 0.145f)), // borravino
            new DecalSpec("Decal_Fichas", "Casino_0038", new Color(0.831f, 0.686f, 0.216f)), // dorado
            new DecalSpec("Decal_Dados", "Casino_0044", new Color(0.106f, 0.165f, 0.357f)),  // azul navy
            new DecalSpec("Decal_Cartas", "Casino_0054", new Color(0.451f, 0.200f, 0.651f)), // violeta
        };

        [MenuItem("Tools/Rollgeon/Bosses/Build Casino Decal Materials")]
        public static void BuildDecalMaterials()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(SourceMaterialPath);
            if (source == null)
            {
                Debug.LogError(LogPrefix + $"No se encontró el material de referencia '{SourceMaterialPath}'.");
                return;
            }

            // El material de referencia usa alpha 0 en _Color; el shadergraph no la lee, pero si
            // alguien la cambia a mano queremos heredarla en vez de imponer 1 en silencio.
            float alpha = source.HasProperty(ColorProperty) ? source.GetColor(ColorProperty).a : 1f;

            int created = 0;
            int updated = 0;
            int untouched = 0;

            foreach (var spec in Specs)
            {
                string destPath = $"{OutputFolder}/{spec.MaterialName}.mat";
                var texture = AssetDatabase.LoadAssetAtPath<Texture>($"{SymbolFolder}/{spec.TextureName}.png");
                if (texture == null)
                {
                    Debug.LogWarning(LogPrefix + $"Falta la textura '{spec.TextureName}.png' — " +
                                     $"'{spec.MaterialName}' salteado.");
                    continue;
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(destPath);
                bool isNew = material == null;
                if (isNew)
                {
                    if (!AssetDatabase.CopyAsset(SourceMaterialPath, destPath))
                    {
                        Debug.LogError(LogPrefix + $"No se pudo clonar el material a '{destPath}'.");
                        continue;
                    }
                    material = AssetDatabase.LoadAssetAtPath<Material>(destPath);
                    if (material == null)
                    {
                        Debug.LogError(LogPrefix + $"El clon '{destPath}' no se pudo cargar.");
                        continue;
                    }
                }

                var color = new Color(spec.Color.r, spec.Color.g, spec.Color.b, alpha);
                bool changed = ApplySpec(material, spec.MaterialName, texture, color);

                if (isNew) created++;
                else if (changed) updated++;
                else untouched++;

                if (changed) EditorUtility.SetDirty(material);
            }

            if (created > 0 || updated > 0) AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"{created} material(es) creado(s), {updated} actualizado(s), " +
                      $"{untouched} sin cambios en {OutputFolder}.");
        }

        private static bool ApplySpec(Material material, string materialName, Texture texture, Color color)
        {
            bool changed = false;

            // CopyAsset copia el archivo tal cual: el m_Name del clon sigue diciendo
            // "DecalHerradura" aunque el asset se llame distinto. Cosmético, pero confunde a
            // cualquiera que lea material.name en un log o en un tool.
            if (material.name != materialName)
            {
                material.name = materialName;
                changed = true;
            }

            if (!material.HasProperty(BaseMapProperty))
            {
                Debug.LogWarning(LogPrefix + $"'{materialName}' no expone '{BaseMapProperty}' — " +
                                 "¿cambió el shadergraph de decals?");
            }
            else if (material.GetTexture(BaseMapProperty) != texture)
            {
                material.SetTexture(BaseMapProperty, texture);
                changed = true;
            }

            if (!material.HasProperty(ColorProperty))
            {
                Debug.LogWarning(LogPrefix + $"'{materialName}' no expone '{ColorProperty}' — sin tintar.");
            }
            else if (material.GetColor(ColorProperty) != color)
            {
                material.SetColor(ColorProperty, color);
                changed = true;
            }

            return changed;
        }
    }
}
