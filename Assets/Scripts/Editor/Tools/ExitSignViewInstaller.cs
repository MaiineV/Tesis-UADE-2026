using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Agrega <see cref="DoorExitSignView"/> al root del <c>DoorBoss.prefab</c> y le asigna
    /// el hijo <c>ExitSign</c>. Un solo prefab cubre las 3 boss rooms y las puertas
    /// swapeadas hacia la boss room (todas anidan DoorBoss). Re-ejecutable: skipea si la
    /// view ya está wireada.
    /// </summary>
    public static class ExitSignViewInstaller
    {
        private const string BossDoorPath = "Assets/Prefabs/Tiles/DoorBoss.prefab";
        private const string SignChildName = "ExitSign";

        [MenuItem("Rollgeon/Tools/Wire Exit Sign View (DoorBoss)")]
        public static void Install()
        {
            var root = PrefabUtility.LoadPrefabContents(BossDoorPath);
            if (root == null)
            {
                Debug.LogError($"[ExitSignViewInstaller] No se pudo abrir '{BossDoorPath}'.");
                return;
            }

            try
            {
                var sign = FindChildByName(root.transform, SignChildName);
                if (sign == null)
                {
                    Debug.LogError($"[ExitSignViewInstaller] '{BossDoorPath}' no tiene un hijo " +
                                   $"'{SignChildName}' — agregarlo al prefab antes de correr el installer.");
                    return;
                }

                var view = root.GetComponent<DoorExitSignView>();
                if (view != null && view.EditorSign == sign.gameObject)
                {
                    Debug.Log("[ExitSignViewInstaller] DoorBoss ya tiene la view wireada — nada que hacer.");
                    return;
                }

                if (view == null) view = root.AddComponent<DoorExitSignView>();

                var so = new SerializedObject(view);
                so.FindProperty(DoorExitSignView.EditorSignField).objectReferenceValue = sign.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BossDoorPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ExitSignViewInstaller] DoorExitSignView wireada en '{BossDoorPath}' " +
                          $"(_sign → '{SignChildName}').");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(includeInactive: true))
                if (t != root && t.name == name)
                    return t;
            return null;
        }
    }
}
