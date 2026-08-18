using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Asigna <c>DoorBoss.prefab</c> al campo <see cref="RoomLayout.BossDoorPrefab"/> de
    /// todos los prefabs de sala del proyecto. El swap real lo hace el
    /// <c>DungeonManager</c> en runtime cuando el vecino de un slot es la boss room —
    /// este installer solo cablea la referencia. Re-ejecutable: skipea salas ya wireadas.
    /// </summary>
    public static class BossDoorPrefabInstaller
    {
        private const string BossDoorPath = "Assets/Prefabs/Tiles/DoorBoss.prefab";
        private static readonly string[] SearchFolders = { "Assets/Prefabs" };

        [MenuItem("Rollgeon/Tools/Wire Boss Door Prefab (All Rooms)")]
        public static void Install()
        {
            var bossDoor = AssetDatabase.LoadAssetAtPath<GameObject>(BossDoorPath);
            if (bossDoor == null)
            {
                Debug.LogError($"[BossDoorPrefabInstaller] No se encontró '{BossDoorPath}'.");
                return;
            }

            int rooms = 0, wired = 0, skipped = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", SearchFolders))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == BossDoorPath) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var layout = root.GetComponent<RoomLayout>();
                    if (layout == null) continue;

                    rooms++;
                    if (layout.BossDoorPrefab == bossDoor)
                    {
                        skipped++;
                        continue;
                    }

                    layout.BossDoorPrefab = bossDoor;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    wired++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[BossDoorPrefabInstaller] {rooms} salas encontradas — " +
                      $"{wired} wireadas, {skipped} ya tenían la referencia.");
        }
    }
}
