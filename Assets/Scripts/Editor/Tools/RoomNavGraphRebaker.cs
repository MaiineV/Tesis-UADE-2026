using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Rebakea el NavGraph serializado de todos los prefabs de sala. Necesario
    /// cuando cambia la lógica de <see cref="NavGraphBaker"/> (ej. BUG-012:
    /// bloqueo por Footprint en vez de renderer bounds) — los prefabs guardan
    /// el graph baked, no lo recalculan en runtime.
    /// </summary>
    public static class RoomNavGraphRebaker
    {
        [MenuItem("Rollgeon/Tools/Rebake Room NavGraphs")]
        public static void RebakeAll()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
            int baked = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var layout = root.GetComponent<RoomLayout>();
                    // Solo rebake de rooms que ya tenían graph baked — crear uno
                    // donde nunca hubo cambiaría "sin restricciones" por un graph real.
                    if (layout == null || layout.NavGraph == null || layout.NavGraph.IsEmpty)
                        continue;

                    layout.NavGraph = NavGraphBaker.Bake(root, layout.BakeSettings);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    baked++;
                    Debug.Log($"[RoomNavGraphRebaker] {path}: {layout.NavGraph.NodeCount} nodes, " +
                              $"{layout.NavGraph.Edges.Count} edges.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[RoomNavGraphRebaker] Rebaked {baked} room prefabs.");
        }
    }
}
