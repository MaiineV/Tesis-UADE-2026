using UnityEditor;
using UnityEngine;
using Rollgeon.Dungeon.Components;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.UI.Tooltips;

namespace Rollgeon.EditorTools.Tooltips
{
    /// <summary>
    /// Tool one-shot: itera los prefabs en <c>Assets/Prefabs/Rooms</c>, asegura que cada
    /// <see cref="DoorController"/> tenga <see cref="WorldTooltipTrigger"/> en cada GO
    /// con Collider y un <see cref="HeroActionTooltipBinder"/> en el root configurado
    /// para Forzar Puerta. Reemplaza el <see cref="DoorTooltipBinder"/> legacy si existe.
    /// Idempotente.
    /// </summary>
    public static class AddTooltipsToDoorPrefabsTool
    {
        private const string PrefabFolder = "Assets/Prefabs/Rooms";

        [MenuItem("Rollgeon/Fix/Add Tooltip Components To Door Prefabs")]
        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
            int prefabsTouched = 0;
            int doorsTouched = 0;
            int triggersAdded = 0;
            int bindersAdded = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                try
                {
                    bool changed = false;
                    var doors = root.GetComponentsInChildren<DoorController>(includeInactive: true);
                    foreach (var door in doors)
                    {
                        // UN solo trigger en el root del DoorController (siempre active).
                        // El raycast manual del trigger detecta hits en cualquier descendant.
                        // Cleanup: remover triggers que hayan quedado en meshes hijos por
                        // versiones anteriores del tool.
                        foreach (var stale in door.GetComponentsInChildren<WorldTooltipTrigger>(includeInactive: true))
                        {
                            if (stale == null) continue;
                            if (stale.gameObject == door.gameObject) continue;
                            Object.DestroyImmediate(stale, allowDestroyingAssets: true);
                            changed = true;
                        }
                        if (EnsureComponent<WorldTooltipTrigger>(door.gameObject))
                        {
                            triggersAdded++;
                            changed = true;
                        }

                        // Binder genérico en el root, configurado para ForceDoor.
                        var binder = door.GetComponent<HeroActionTooltipBinder>();
                        if (binder == null)
                        {
                            binder = door.gameObject.AddComponent<HeroActionTooltipBinder>();
                            bindersAdded++;
                            changed = true;
                        }

                        // Setear via SerializedObject para que persista en el prefab.
                        var so = new SerializedObject(binder);
                        var slotProp = so.FindProperty("_slot");
                        var phaseProp = so.FindProperty("_resolvePhase");
                        var combatProp = so.FindProperty("_onlyDuringCombat");

                        if (slotProp != null && slotProp.intValue != (int)HeroBehaviorSlot.ForceDoor)
                        {
                            slotProp.intValue = (int)HeroBehaviorSlot.ForceDoor;
                            changed = true;
                        }
                        if (phaseProp != null && phaseProp.intValue != (int)GamePhase.Combat)
                        {
                            phaseProp.intValue = (int)GamePhase.Combat;
                            changed = true;
                        }
                        if (combatProp != null && !combatProp.boolValue)
                        {
                            combatProp.boolValue = true;
                            changed = true;
                        }
                        so.ApplyModifiedPropertiesWithoutUndo();

                        doorsTouched++;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabsTouched++;
                        Debug.Log($"[AddTooltipsToDoorPrefabs] Updated '{path}'");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[AddTooltipsToDoorPrefabs] Done. Prefabs modificados: {prefabsTouched}. " +
                      $"Doors visitadas: {doorsTouched}. WorldTooltipTrigger nuevos: {triggersAdded}. " +
                      $"HeroActionTooltipBinder nuevos: {bindersAdded}.");
        }

        private static bool EnsureComponent<T>(GameObject go) where T : Component
        {
            if (go.GetComponent<T>() != null) return false;
            go.AddComponent<T>();
            return true;
        }
    }
}
