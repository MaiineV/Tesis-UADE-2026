using Rollgeon.Tiles;
using Rollgeon.Tiles.Visuals;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Tiles
{
    /// <summary>
    /// Wirea los prefabs de arte de <c>Art/3D/Models/Items</c> a sus <c>Tile_*.asset</c>
    /// (generaliza <see cref="SpikeTileVisualInstaller"/>). El commit 82c2f8f0 trajo los
    /// prefabs pero quedaron huérfanos: 8 de las 12 casillas seguían degradando al overlay
    /// tintado.
    /// </summary>
    /// <remarks>
    /// Sin tooltip ni collider en los prefabs, a propósito: es el patrón de Spikes — un
    /// collider en el visual podría interceptar el raycast del <c>TileClickHandler</c>.
    /// Fuego NO se toca (usa <c>VFX_Fire.prefab</c>, decisión del 23/08); SafeZone y
    /// Telegraph quedan con overlay como fallback deliberado.
    /// </remarks>
    public static class SpecialTileVisualsInstaller
    {
        private const string ItemsFolder = "Assets/Art/3D/Models/Items";
        private const string TilesFolder = "Assets/Rollgeon/Tiles";

        // (prefab, tile asset). El de stun se llama WaterElectricTile pero la casilla del
        // catálogo es el Charco Eléctrico; Venom viste al tile de Veneno.
        private static readonly (string prefab, string tile)[] Mapping =
        {
            ("BoostTile", "Tile_Boost"),
            ("HealTile", "Tile_Heal"),
            ("Ice", "Tile_Ice"),
            ("Portal", "Tile_Portal"),
            ("StrengthTile", "Tile_Strength"),
            ("Venom", "Tile_Poison"),
            ("WaterElectricTile", "Tile_ElectricPuddle"),
        };

        [MenuItem("Tools/Rollgeon/Tiles/Install Tile Visuals")]
        public static void Install()
        {
            int wired = 0;
            foreach (var (prefabName, tileName) in Mapping)
            {
                string prefabPath = $"{ItemsFolder}/{prefabName}.prefab";
                string tilePath = $"{TilesFolder}/{tileName}.asset";

                var prefab = EnsureBinding(prefabPath);
                if (prefab == null) continue;

                var tile = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(tilePath);
                if (tile == null)
                {
                    Debug.LogError($"[TileVisuals] No está '{tilePath}' — correr Rollgeon/Tiles/Setup All primero.");
                    continue;
                }

                tile.VisualPrefab = prefab;
                tile.VisualYOffset = 0.02f;

                // El tint es el respaldo para cuando NO hay malla; con malla, un blanco a
                // 0.35 lava el arte (mismo motivo documentado en SpikeTileVisualInstaller).
                tile.OverlayTint = new Color(0f, 0f, 0f, 0f);

                EditorUtility.SetDirty(tile);
                wired++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TileVisuals] {wired}/{Mapping.Length} casillas wireadas a sus prefabs de Items.");
        }

        /// <summary>
        /// Garantiza el <see cref="SpecialTileVisualBinding"/> en el prefab: sin él, el
        /// visual nunca se entera de armado/disparo/expiry.
        /// </summary>
        private static GameObject EnsureBinding(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[TileVisuals] No está el prefab '{prefabPath}'.");
                return null;
            }

            if (prefab.GetComponent<SpecialTileVisualBinding>() != null) return prefab;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (root.GetComponent<SpecialTileVisualBinding>() == null)
                    root.AddComponent<SpecialTileVisualBinding>();
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
