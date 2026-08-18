using System.Text;
using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Repara <see cref="RoomLayout.DoorSlots"/> que quedaron apuntando a un DoorRoot
    /// DESACTIVADO — el caso de las boss rooms donde se reemplazaron las 4 puertas por
    /// instancias de DoorBoss pero los slots siguieron referenciando las Door viejas
    /// (desactivadas como rollback). Por cada slot roto busca el DoorController ACTIVO
    /// cuya posición infiere la misma dirección (misma fuente de verdad que
    /// Auto-Populate: <see cref="RoomLayout.InferDoorDirection"/>) y re-apunta
    /// DoorRoot/Anchor/WallPlug. No toca las puertas viejas desactivadas.
    /// Re-ejecutable: los slots ya bindeados a puertas activas se skipean.
    /// </summary>
    public static class DoorSlotRebinder
    {
        private static readonly string[] SearchFolders = { "Assets/Prefabs/Rooms" };

        [MenuItem("Rollgeon/Tools/Rebind Door Slots To Active Doors")]
        public static void Rebind()
        {
            var sb = new StringBuilder("[DoorSlotRebinder] Reporte:");
            int roomsTouched = 0, slotsRebound = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", SearchFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var layout = root.GetComponent<RoomLayout>();
                    if (layout == null || layout.DoorSlots == null) continue;

                    int reboundHere = RebindLayout(layout, path, sb);
                    if (reboundHere > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        roomsTouched++;
                        slotsRebound += reboundHere;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            sb.Append($"\n— {roomsTouched} salas reparadas, {slotsRebound} slots rebindeados.");
            Debug.Log(sb.ToString());
        }

        private static int RebindLayout(RoomLayout layout, string path, StringBuilder sb)
        {
            var controllers = layout.GetComponentsInChildren<DoorController>(includeInactive: true);
            int rebound = 0;

            foreach (var slot in layout.DoorSlots)
            {
                if (slot == null) continue;
                // Slot sano: su DoorRoot existe y está activo — nada que reparar.
                if (slot.DoorRoot != null && slot.DoorRoot.activeSelf) continue;

                DoorController match = null;
                foreach (var ctrl in controllers)
                {
                    if (!ctrl.gameObject.activeSelf) continue;
                    if (layout.InferDoorDirection(ctrl.transform.position) != slot.Direction) continue;
                    match = ctrl;
                    break;
                }

                if (match == null)
                {
                    sb.Append($"\n  {path}: slot {slot.Direction} tiene DoorRoot inactivo/nulo " +
                              "y no hay DoorController activo en esa dirección — revisar a mano.");
                    continue;
                }

                slot.DoorRoot = match.gameObject;
                // Mismo contrato que AutoPopulateDoorSlots: Anchor = transform del
                // controller (lo comparten spawn y DoorTileQuery) y la reja del propio root.
                slot.Anchor = match.transform;
                slot.WallPlug = match.WallPlugRef;
                match.Direction = slot.Direction;
                match.SpawnPointId = slot.Direction.DoorStateKey();

                sb.Append($"\n  {path}: slot {slot.Direction} → '{match.gameObject.name}' (activo).");
                rebound++;
            }

            return rebound;
        }
    }
}
