using System.Collections.Generic;
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
        // Solo salas vivas. OLD/ tiene prefabs muertos que no deben re-serializarse.
        private static readonly string[] RoomFolders =
        {
            "Assets/Prefabs/Rooms/FloorOne",
            "Assets/Prefabs/Rooms/FloorTwo",
        };

        [MenuItem("Rollgeon/Tools/Rebake Room NavGraphs")]
        public static void RebakeAll()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", RoomFolders);
            int baked = 0;
            var findings = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var layout = root.GetComponent<RoomLayout>();
                    if (layout == null) continue;

                    // Antes se salteaban las salas con NavGraph vacío para no convertir
                    // "sin restricciones" en un graph real sin querer. Pero eso dejaba las
                    // salas nuevas (FloorTwo) sin bakear y con el cruce de puertas roto en
                    // silencio. Ahora se bakean igual, avisando que arrancaban vacías.
                    bool wasEmpty = layout.NavGraph == null || layout.NavGraph.IsEmpty;
                    if (wasEmpty)
                        Debug.LogWarning($"[RoomNavGraphRebaker] {path}: NavGraph vacío (sala nueva sin bakear). Bakeando ahora.");

                    layout.NavGraph = NavGraphBaker.Bake(root, layout.BakeSettings);

                    foreach (var f in RoomDoorBakeValidator.ValidateRoom(layout))
                        findings.Add($"{path}: {f}");

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
            if (findings.Count > 0)
                Debug.LogWarning(
                    $"[RoomNavGraphRebaker] {findings.Count} problema(s) de puertas tras rebake:\n• " +
                    string.Join("\n• ", findings));
        }
    }
}
