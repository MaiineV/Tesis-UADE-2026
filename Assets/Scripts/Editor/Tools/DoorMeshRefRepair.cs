using System.Text;
using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Repara <see cref="DoorController"/>s cuyos campos de mesh
    /// (<c>_meshOpen/_meshClosed/_wallPlug/_meshWallFill</c>) quedaron apuntando a meshes
    /// de OTRA puerta — el caso de las boss rooms, donde las instancias de DoorBoss
    /// heredaron por override las referencias de la Door vieja desactivada (paste de
    /// component values / reemplazo manual). Con eso, SetState togglea los meshes de la
    /// puerta invisible y la DoorBoss queda clavada en su default (reja siempre visible).
    /// <para>
    /// Invariante reparada: cada referencia debe ser descendiente del GO del propio
    /// controller. Si no lo es (o es null), se re-apunta al hijo por nombre
    /// (MeshOpen/MeshClose/WallPlug/WallFill). Re-ejecutable: los controllers sanos se skipean.
    /// </para>
    /// </summary>
    public static class DoorMeshRefRepair
    {
        private static readonly string[] SearchFolders = { "Assets/Prefabs/Rooms" };

        private static readonly (string field, string childName)[] Wiring =
        {
            (DoorController.EditorMeshOpenField,     "MeshOpen"),
            (DoorController.EditorMeshClosedField,   "MeshClose"),
            (DoorController.EditorWallPlugField,     "WallPlug"),
            (DoorController.EditorMeshWallFillField, "WallFill"),
        };

        [MenuItem("Rollgeon/Tools/Repair Door Mesh References")]
        public static void Repair()
        {
            var sb = new StringBuilder("[DoorMeshRefRepair] Reporte:");
            int roomsTouched = 0, controllersFixed = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", SearchFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int fixedHere = 0;
                    foreach (var ctrl in root.GetComponentsInChildren<DoorController>(includeInactive: true))
                        if (RepairController(ctrl, path, sb))
                            fixedHere++;

                    if (fixedHere > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        roomsTouched++;
                        controllersFixed += fixedHere;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            sb.Append($"\n— {roomsTouched} salas reparadas, {controllersFixed} DoorControllers re-wireados.");
            Debug.Log(sb.ToString());
        }

        private static bool RepairController(DoorController ctrl, string path, StringBuilder sb)
        {
            var so = new SerializedObject(ctrl);
            bool changed = false;

            foreach (var (field, childName) in Wiring)
            {
                var prop = so.FindProperty(field);
                var current = prop.objectReferenceValue as GameObject;
                if (IsOwnDescendant(ctrl, current)) continue;

                var target = FindChildByName(ctrl.transform, childName);
                if (target == null)
                {
                    sb.Append($"\n  {path}: '{ctrl.gameObject.name}' ({ctrl.Direction}) sin hijo " +
                              $"'{childName}' — no se pudo reparar {field}.");
                    continue;
                }

                prop.objectReferenceValue = target.gameObject;
                sb.Append($"\n  {path}: '{ctrl.gameObject.name}' ({ctrl.Direction}) {field} " +
                          $"apuntaba {(current == null ? "a null" : $"a '{current.name}' ajeno")} → '{childName}' propio.");
                changed = true;
            }

            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool IsOwnDescendant(DoorController ctrl, GameObject reference) =>
            reference != null && reference.transform.IsChildOf(ctrl.transform);

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                if (t != root && t.name == name)
                    return t;
            return null;
        }
    }
}
