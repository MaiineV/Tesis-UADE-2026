using System.Collections.Generic;
using System.Text;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Auditoría y reparación en bloque del "bake doors" de todas las salas vivas
    /// (FloorOne/FloorTwo). Diagnose es read-only (un reporte para leer antes de tocar);
    /// Repair re-corre Auto-Populate + Bake NavGraph + validación y guarda cada prefab.
    /// </summary>
    public static class RoomDoorDiagnostics
    {
        private static readonly string[] RoomFolders =
        {
            "Assets/Prefabs/Rooms/FloorOne",
            "Assets/Prefabs/Rooms/FloorTwo",
        };

        [MenuItem("Rollgeon/Tools/Diagnose Room Doors (FloorOne+FloorTwo)")]
        public static void Diagnose()
        {
            var sb = new StringBuilder("[RoomDoorDiagnostics] Reporte (read-only, no se modifica nada):");
            int rooms = 0, withIssues = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", RoomFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var layout = root.GetComponent<RoomLayout>();
                    if (layout == null) continue;
                    rooms++;
                    sb.Append(BuildRoomReport(layout, path, out bool hasIssues));
                    if (hasIssues) withIssues++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            sb.Append($"\n\n— {rooms} salas, {withIssues} con findings.");
            Debug.Log(sb.ToString());
        }

        [MenuItem("Rollgeon/Tools/Repair Room Doors (Auto-Populate + Bake + Validate)")]
        public static void Repair()
        {
            var sb = new StringBuilder("[RoomDoorDiagnostics] Repair (Auto-Populate + Bake + Validate):");
            int repaired = 0, stillWithIssues = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", RoomFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var layout = root.GetComponent<RoomLayout>();
                    if (layout == null) continue;

                    layout.AutoPopulateDoorSlots();
                    layout.NavGraph = NavGraphBaker.Bake(root, layout.BakeSettings);
                    var findings = RoomDoorBakeValidator.ValidateRoom(layout);

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    repaired++;

                    sb.Append($"\n{path}: {layout.DoorSlots.Count} slots, {layout.NavGraph.NodeCount} nodes.");
                    if (findings.Count > 0)
                    {
                        stillWithIssues++;
                        sb.Append("\n  ! " + string.Join("\n  ! ", findings));
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            sb.Append($"\n\n— Reparadas {repaired} salas; {stillWithIssues} aún con findings " +
                      "(requieren pintar el tile-frente como Floor a mano y volver a correr Repair).");
            Debug.Log(sb.ToString());
            AssetDatabase.Refresh();
        }

        private static string BuildRoomReport(RoomLayout layout, string path, out bool hasIssues)
        {
            hasIssues = false;
            var sb = new StringBuilder($"\n\n{path}");

            var controllers = layout.GetComponentsInChildren<DoorController>(true);
            var ctrlDirs = new HashSet<DoorDirection>();
            foreach (var c in controllers) ctrlDirs.Add(c.Direction);

            var slotDirs = new HashSet<DoorDirection>();
            foreach (var s in layout.DoorSlots) if (s != null) slotDirs.Add(s.Direction);

            sb.Append($"\n  slots=[{Join(slotDirs)}] controllers=[{Join(ctrlDirs)}] " +
                      $"navgraph={(layout.NavGraph == null || layout.NavGraph.IsEmpty ? "VACÍO" : layout.NavGraph.NodeCount + " nodes")}");

            if (layout.DoorSlots == null || layout.DoorSlots.Count == 0)
            {
                hasIssues = true;
                sb.Append("\n  ! sin DoorSlots autorados (correr Auto-Populate / Repair).");
            }

            // Controller cuya dirección no tiene slot: el DungeonManager lo tapiaría y avisaría.
            foreach (var c in controllers)
            {
                if (!slotDirs.Contains(c.Direction))
                {
                    hasIssues = true;
                    sb.Append($"\n  ! DoorController dir={c.Direction} ('{c.name}') sin DoorSlotRef.");
                }
            }

            foreach (var f in RoomDoorBakeValidator.ValidateRoom(layout))
            {
                hasIssues = true;
                sb.Append($"\n  ! {f}");
            }

            if (!hasIssues) sb.Append("\n  ok");
            return sb.ToString();
        }

        private static string Join(HashSet<DoorDirection> dirs)
        {
            if (dirs.Count == 0) return "—";
            var list = new List<string>(dirs.Count);
            foreach (var d in dirs) list.Add(d.ToString());
            list.Sort();
            return string.Join(",", list);
        }
    }
}
