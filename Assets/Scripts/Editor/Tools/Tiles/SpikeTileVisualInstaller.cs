using Rollgeon.Tiles;
using Rollgeon.Tiles.Visuals;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Tiles
{
    /// <summary>
    /// Envuelve <c>Spikes.fbx</c> en un prefab y se lo asigna al tile de pinchos.
    /// </summary>
    /// <remarks>
    /// El arte llegó al repo el 2026-08-21 y quedó sin envolver, así que
    /// <c>Tile_Spikes.asset</c> seguía con <c>VisualPrefab</c> nulo. Una casilla especial sin visual
    /// no falla: degrada a un quad con el <c>OverlayTint</c>, y como ese tint estaba en blanco se veía
    /// un piso gris. El del Cajero hereda el visual de este, así que arreglar el genérico arregla los dos.
    /// </remarks>
    public static class SpikeTileVisualInstaller
    {
        public const string SpikeFbxPath = "Assets/Art/3D/Models/Items/Spikes.fbx";
        public const string SpikePrefabPath = "Assets/Art/3D/Models/Items/Spikes.prefab";
        public const string GenericSpikeTilePath = "Assets/Rollgeon/Tiles/Tile_Spikes.asset";

        /// <summary>Alto del pincho armado sobre el piso. El tile suma su propio VisualYOffset.</summary>
        private const float SunkDepth = 0.32f;

        [MenuItem("Tools/Rollgeon/Tiles/Install Spike Visual")]
        public static void Install()
        {
            var prefab = EnsureSpikePrefab();
            if (prefab == null) return;

            var tile = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(GenericSpikeTilePath);
            if (tile == null)
            {
                Debug.LogError($"[SpikeTileVisualInstaller] No está '{GenericSpikeTilePath}'.");
                return;
            }

            tile.VisualPrefab = prefab;
            tile.VisualYOffset = 0.02f;

            // El tint es el respaldo para cuando NO hay malla. Con malla, un blanco a 0.35 lava el
            // arte y vuelve a dar el piso gris que motivó todo esto.
            tile.OverlayTint = new Color(0f, 0f, 0f, 0f);

            EditorUtility.SetDirty(tile);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SpikeTileVisualInstaller] '{tile.name}' ahora usa '{prefab.name}'. " +
                      "Correr 'Tools → Rollgeon → Bosses → Build Cajero' para propagarlo a los suyos.");
        }

        /// <summary>
        /// Crea (o rehace) el prefab de pinchos: la malla del FBX más el binding de estado y el
        /// componente que hunde el pincho disparado.
        /// </summary>
        public static GameObject EnsureSpikePrefab()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SpikeFbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[SpikeTileVisualInstaller] No está el arte en '{SpikeFbxPath}'.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            if (instance == null)
            {
                Debug.LogError("[SpikeTileVisualInstaller] No se pudo instanciar el FBX.");
                return null;
            }

            try
            {
                instance.name = "Spikes";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;

                // El binding lo bindea SpecialTileService al instanciar el visual por celda; sin él
                // el pincho nunca se entera de que se disparó.
                if (instance.GetComponent<SpecialTileVisualBinding>() == null)
                    instance.AddComponent<SpecialTileVisualBinding>();

                var armed = instance.GetComponent<SpikeArmedVisual>();
                if (armed == null) armed = instance.AddComponent<SpikeArmedVisual>();
                armed.SunkDepth = SunkDepth;

                // Se hunde la malla, no el root: el root lo posiciona el servicio en su celda.
                var mesh = instance.GetComponentInChildren<MeshRenderer>();
                armed.Spikes = mesh != null ? mesh.transform : instance.transform;

                if (mesh == null)
                {
                    Debug.LogWarning("[SpikeTileVisualInstaller] El FBX no trajo MeshRenderer: el " +
                                     "prefab queda vacío y los pinchos se siguen viendo como el piso.");
                }

                // Todo mutado ANTES de guardar: SaveAsPrefabAsset serializa lo que hay ahora, y las
                // mutaciones posteriores a la creación del asset se pierden.
                return PrefabUtility.SaveAsPrefabAsset(instance, SpikePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
