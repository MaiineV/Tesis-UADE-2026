using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Vuelve a apuntar los materiales de un prefab de arte a los que hoy expone su FBX de origen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cuando un FBX deja de generar sus materiales como sub-assets y pasa a los <c>Mat_*</c> del
    /// proyecto, los prefabs armados sobre él siguen guardando esos sub-assets por <c>fileID</c> y
    /// Unity los resuelve a <c>null</c> sin avisar: el prefab abre y anima, pero llega a Play sin
    /// materiales.
    /// </para>
    /// <para>
    /// El match es por <i>ruta de jerarquía</i> desde la raíz y con fallback a nombre suelto: los
    /// prefabs animados suelen tener un padre extra sobre la jerarquía cruda del FBX. Un renderer
    /// que no matchea se reporta y se deja intacto.
    /// </para>
    /// </remarks>
    public static class ArtPrefabMaterialRepointer
    {
        [MenuItem("Tools/Rollgeon/Art/Repoint DiceBoss Materials")]
        public static void RepointDiceBoss()
        {
            Repoint("Assets/Prefabs/Enemies/DiceBoss_Animated.prefab",
                    "Assets/Art/3D/Models/Enemies/DiceBoss_Model.fbx");
        }

        /// <summary>
        /// Copia la asignación de materiales de <paramref name="modelPath"/> sobre
        /// <paramref name="prefabPath"/>. No toca nada más del prefab.
        /// </summary>
        /// <returns>Cantidad de slots reasignados.</returns>
        public static int Repoint(string prefabPath, string modelPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"[ArtPrefabMaterialRepointer] No hay modelo en '{modelPath}'.");
                return 0;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[ArtPrefabMaterialRepointer] No hay prefab en '{prefabPath}'.");
                return 0;
            }

            var source = CollectRenderers(model);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            int repointed = 0;
            var unmatched = new List<string>();

            try
            {
                foreach (var target in root.GetComponentsInChildren<Renderer>(true))
                {
                    string path = HierarchyPath(target.transform, root.transform);
                    if (!source.TryGetValue(path, out var materials) &&
                        !source.TryGetValue(target.name, out materials))
                    {
                        unmatched.Add(path);
                        continue;
                    }

                    // El modelo manda también en la cantidad de slots: si el FBX cambió de submeshes,
                    // conservar los sobrantes dejaría materiales apuntando a submeshes que no existen.
                    target.sharedMaterials = materials;
                    repointed += materials.Length;
                }

                if (repointed > 0) PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Report(prefabPath, modelPath, repointed, unmatched);
            return repointed;
        }

        /// <summary>Renderers del modelo indexados por ruta y, además, por nombre suelto.</summary>
        private static Dictionary<string, Material[]> CollectRenderers(GameObject model)
        {
            var byPath = new Dictionary<string, Material[]>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                byPath[HierarchyPath(renderer.transform, model.transform)] = materials;

                // El nombre suelto es el fallback y sólo sirve si es único: con dos "Body" no hay
                // forma de saber cuál es cuál, así que se prefiere no matchear a matchear mal.
                if (byPath.ContainsKey(renderer.name)) byPath[renderer.name] = null;
                else byPath[renderer.name] = materials;
            }

            foreach (var key in new List<string>(byPath.Keys))
                if (byPath[key] == null) byPath.Remove(key);

            return byPath;
        }

        private static string HierarchyPath(Transform node, Transform root)
        {
            var sb = new StringBuilder(node.name);
            for (var t = node.parent; t != null && t != root; t = t.parent)
                sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        private static void Report(string prefabPath, string modelPath, int repointed,
                                   List<string> unmatched)
        {
            if (repointed == 0)
            {
                Debug.LogWarning($"[ArtPrefabMaterialRepointer] Ningún renderer de '{prefabPath}' " +
                                 $"matcheó con '{modelPath}' — el prefab queda como estaba.");
            }
            else
            {
                Debug.Log($"[ArtPrefabMaterialRepointer] '{prefabPath}': {repointed} slot(s) " +
                          $"reapuntados a los materiales de '{modelPath}'.");
            }

            if (unmatched.Count > 0)
            {
                Debug.LogWarning($"[ArtPrefabMaterialRepointer] Sin equivalente en el modelo, se " +
                                 $"dejaron intactos: {string.Join(", ", unmatched)}.");
            }
        }
    }
}
