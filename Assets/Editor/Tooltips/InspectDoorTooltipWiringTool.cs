using UnityEditor;
using UnityEngine;
using Rollgeon.Dungeon.Components;
using Rollgeon.UI.Tooltips;

namespace Rollgeon.EditorTools.Tooltips
{
    /// <summary>
    /// Diagnóstico: loguea por cada DoorController en los prefabs de sala qué
    /// componentes de tooltip tiene cableados y con qué config. One-shot de
    /// inspección — no modifica nada.
    /// </summary>
    public static class InspectDoorTooltipWiringTool
    {
        private const string PrefabFolder = "Assets/Prefabs/Rooms";

        [MenuItem("Rollgeon/Diagnose/Inspect Door Tooltip Wiring")]
        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    Debug.Log($"=== {path} ===");
                    var doors = root.GetComponentsInChildren<DoorController>(includeInactive: true);
                    foreach (var door in doors)
                    {
                        var goPath = GetGameObjectPath(door.transform);
                        var binder = door.GetComponent<HeroActionTooltipBinder>();
                        var triggers = door.GetComponentsInChildren<WorldTooltipTrigger>(includeInactive: true);
                        var colliders = door.GetComponentsInChildren<Collider>(includeInactive: true);

                        string binderInfo = binder != null ? DescribeBinder(binder) : "MISSING";
                        Debug.Log($"  Door '{goPath}' dir={door.Direction} | " +
                                  $"binder={binderInfo} | triggers={triggers.Length} | " +
                                  $"colliders={colliders.Length}");

                        for (int i = 0; i < triggers.Length; i++)
                        {
                            var t = triggers[i];
                            var tPath = GetGameObjectPath(t.transform);
                            var so = new SerializedObject(t);
                            var modeProp = so.FindProperty("_mode");
                            var camProp = so.FindProperty("_camera");
                            string mode = modeProp != null ? ((WorldTooltipMode)modeProp.enumValueIndex).ToString() : "?";
                            bool hasOwnCollider = t.GetComponent<Collider>() != null;
                            Debug.Log($"    trigger#{i} on '{tPath}' mode={mode} hasOwnCollider={hasOwnCollider} cam={(camProp?.objectReferenceValue != null ? "yes" : "main")}");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static string DescribeBinder(HeroActionTooltipBinder binder)
        {
            var so = new SerializedObject(binder);
            var slot = so.FindProperty("_slot");
            var phase = so.FindProperty("_resolvePhase");
            var combat = so.FindProperty("_onlyDuringCombat");
            return $"slot={(slot != null ? slot.enumDisplayNames[slot.enumValueIndex] : "?")} " +
                   $"phase={(phase != null ? phase.enumDisplayNames[phase.enumValueIndex] : "?")} " +
                   $"onlyCombat={(combat != null ? combat.boolValue.ToString() : "?")}";
        }

        private static string GetGameObjectPath(Transform t)
        {
            if (t == null) return "<null>";
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
