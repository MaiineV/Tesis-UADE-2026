using System.Collections.Generic;
using System.Linq;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.UI.HUD.Status;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.HUD
{
    /// <summary>
    /// Autora <c>StatusIconCatalog.asset</c> con los 8 slices de <c>statuseffects.png</c> y
    /// deja el catálogo referenciado en el prefab <c>Canvas_PlayerStatus</c> (hasta ahora
    /// vivía solo como override de escena en 02_Gameplay — deuda que rompía en cualquier
    /// otra escena).
    /// </summary>
    /// <remarks>
    /// El slice 7 (pasiva del warrior activa) NO va al catálogo: la pasiva lee su ícono de
    /// <c>ClassPassiveSO.ActiveIcon</c>, autorado por <c>PlayerIconsSetupTools</c>.
    /// </remarks>
    public static class StatusIconCatalogInstaller
    {
        public const string CatalogPath = "Assets/Rollgeon/Tiles/StatusIconCatalog.asset";
        public const string SheetPath = "Assets/Art/UI/StatusEffects/statuseffects.png";
        public const string PlayerStatusPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_PlayerStatus.prefab";

        // Mapeo cerrado con el usuario (23/08): 0=burn, 1=tp-delay, 2=poison, 3=heal,
        // 4=speed, 5=attack, 6=stun. 2 y 6 reemplazan los placeholders de la demo de Feel.
        private static readonly (string id, string slice)[] Mapping =
        {
            (TileStandStatusProvider.BurnId, "statuseffects_0"),
            (TeleportCooldownStatusProvider.StateId, "statuseffects_1"),
            (PoisonStatusProvider.StateId, "statuseffects_2"),
            (TileStandStatusProvider.HealId, "statuseffects_3"),
            (TileStandStatusProvider.SpeedId, "statuseffects_4"),
            (TileStandStatusProvider.AttackId, "statuseffects_5"),
            (StunStatusProvider.StateId, "statuseffects_6"),

            // Las dos intenciones del Croupier que terminan en fuego reusan el slice de burn:
            // es literalmente el mismo fuego, y sin entry la tarjeta sale sin ícono. Las otras
            // dos (siembra, disparo) siguen sin arte y su tarjeta degrada al título solo.
            (AIIntentTextKeys.Ignite, "statuseffects_0"),
            (AIIntentTextKeys.BombBlast, "statuseffects_0"),
        };

        [MenuItem("Rollgeon/Player Icons/6 - Author Status Icon Catalog")]
        public static void Install()
        {
            // Todo-o-nada: un catálogo a medias (algunos estados con arte nuevo, otros con
            // el placeholder) sería peor que fallar acá con el error a la vista.
            var resolved = new List<(string id, Sprite sprite)>();
            foreach (var (id, slice) in Mapping)
            {
                var sprite = LoadSlice(slice);
                if (sprite == null) return;
                resolved.Add((id, sprite));
            }

            var catalog = AssetDatabase.LoadAssetAtPath<StatusIconCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<StatusIconCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            entries.arraySize = resolved.Count;
            for (int i = 0; i < resolved.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Id").stringValue = resolved[i].id;
                entry.FindPropertyRelative("Icon").objectReferenceValue = resolved[i].sprite;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            AssignCatalogInPrefab(catalog);

            AssetDatabase.SaveAssets();
            Debug.Log($"[StatusIconCatalog] {resolved.Count} entries autoradas desde statuseffects.png " +
                      "y catálogo asignado en Canvas_PlayerStatus. El override de escena en 02_Gameplay " +
                      "apunta al mismo asset — se puede revertir a mano cuando se quiera.");
        }

        private static void AssignCatalogInPrefab(StatusIconCatalogSO catalog)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerStatusPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[StatusIconCatalog] No está el prefab '{PlayerStatusPrefabPath}'.");
                return;
            }

            try
            {
                var view = root.GetComponentInChildren<PlayerStatusIconsView>(true);
                if (view == null)
                {
                    Debug.LogError("[StatusIconCatalog] El prefab no tiene PlayerStatusIconsView.");
                    return;
                }

                var serialized = new SerializedObject(view);
                serialized.FindProperty("_statusIconCatalog").objectReferenceValue = catalog;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PlayerStatusPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite LoadSlice(string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(SheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
            {
                Debug.LogError($"[StatusIconCatalog] Slice '{spriteName}' no encontrado en {SheetPath} — " +
                               "no se autoró nada.");
            }
            return sprite;
        }
    }
}
