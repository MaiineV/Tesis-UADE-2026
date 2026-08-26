using System.IO;
using Rollgeon.UI.HUD.Status;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Autora el asset de settings de la fila de estados que flota sobre cada enemigo.
    /// </summary>
    /// <remarks>
    /// Va en <c>Resources</c> porque la fila la cuelga <c>EntityVisualService</c> al spawnear, y
    /// ese camino no tiene inspector donde cablear nada. Sin el asset el enemigo se queda con su
    /// tooltip de texto y nada más: degrada, no rompe.
    /// </remarks>
    public static class EnemyStatusRowSetupTools
    {
        private const string SettingsPath = "Assets/Resources/UI/EnemyStatusRowSettings.asset";
        private const string IconPrefabPath = "Assets/Prefabs/UI/StatusEffectIcon.prefab";
        private const string CatalogPath = "Assets/Rollgeon/Tiles/StatusIconCatalog.asset";

        [MenuItem("Rollgeon/Tooltips/3 - Author Enemy Status Row Settings")]
        public static void Author()
        {
            var iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IconPrefabPath);
            var iconView = iconPrefab != null ? iconPrefab.GetComponent<StatusEffectIconView>() : null;
            if (iconView == null)
            {
                Debug.LogError($"[EnemyStatusRowSetupTools] Falta {IconPrefabPath} o su StatusEffectIconView.");
                return;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<StatusIconCatalogSO>(CatalogPath);
            if (catalog == null)
                Debug.LogWarning($"[EnemyStatusRowSetupTools] Sin catálogo en {CatalogPath}: los íconos van a salir en blanco.");

            var settings = AssetDatabase.LoadAssetAtPath<EnemyStatusRowSettingsSO>(SettingsPath);
            bool created = settings == null;
            if (created) settings = ScriptableObject.CreateInstance<EnemyStatusRowSettingsSO>();

            settings.IconPrefab = iconView;
            settings.Catalog = catalog;

            if (created)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            else
            {
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[EnemyStatusRowSetupTools] Settings de la fila {(created ? "creados" : "actualizados")} en {SettingsPath}.");
        }
    }
}
