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
        public const string CajeroSpikeTilePath = "Assets/Rollgeon/Tiles/Tile_Spikes_Cajero.asset";

        /// <summary>Alto del pincho armado sobre el piso. El tile suma su propio VisualYOffset.</summary>
        private const float SunkDepth = 0.32f;

        [MenuItem("Tools/Rollgeon/Tiles/Install Spike Visual")]
        public static void Install()
        {
            var prefab = EnsureSpikePrefab();
            if (prefab == null) return;

            // El del Cajero también, y directo: regenerar el prefab cambia el fileID del
            // root, así que cualquier tile que lo referenciara queda con la referencia
            // colgada hasta re-asignarla.
            int wired = 0;
            foreach (var path in new[] { GenericSpikeTilePath, CajeroSpikeTilePath })
            {
                var tile = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(path);
                if (tile == null)
                {
                    Debug.LogWarning($"[SpikeTileVisualInstaller] No está '{path}' — salteado.");
                    continue;
                }

                tile.VisualPrefab = prefab;
                tile.VisualYOffset = 0.02f;

                // El tint es el respaldo para cuando NO hay malla. Con malla, un blanco a 0.35
                // lava el arte y vuelve a dar el piso gris que motivó todo esto.
                tile.OverlayTint = new Color(0f, 0f, 0f, 0f);

                EditorUtility.SetDirty(tile);
                wired++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SpikeTileVisualInstaller] {wired} tiles de pinchos apuntando a '{prefab.name}'.");
        }

        /// <summary>
        /// Crea (o rehace) el prefab de pinchos: un root VACÍO con el binding y el componente
        /// que hunde, más la malla del FBX como HIJO ("Art").
        /// </summary>
        /// <remarks>
        /// La estructura root-vacío + hijo no es cosmética: el root lo posiciona el pool en la
        /// celda, así que <c>SpikeArmedVisual.Spikes</c> DEBE ser un hijo para que sus
        /// localPosition de armado/hundido sean relativas a la celda. La versión anterior
        /// instanciaba el FBX (un solo nodo) como root y la malla terminaba siendo el root
        /// mismo — el primer Sink o reciclaje del pool mandaba el pincho al origen del mundo
        /// (bug de playtest 23/08: "puse pinchos y no aparecieron").
        /// </remarks>
        public static GameObject EnsureSpikePrefab()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(SpikeFbxPath);
            if (fbx == null)
            {
                Debug.LogError($"[SpikeTileVisualInstaller] No está el arte en '{SpikeFbxPath}'.");
                return null;
            }

            var root = new GameObject("Spikes");
            try
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                if (art == null)
                {
                    Debug.LogError("[SpikeTileVisualInstaller] No se pudo instanciar el FBX.");
                    return null;
                }

                art.name = "Art";
                art.transform.SetParent(root.transform, worldPositionStays: false);
                art.transform.localPosition = Vector3.zero;
                art.transform.localRotation = Quaternion.identity;

                // El binding lo bindea SpecialTileService al instanciar el visual por celda; sin él
                // el pincho nunca se entera de que se disparó.
                root.AddComponent<SpecialTileVisualBinding>();

                var armed = root.AddComponent<SpikeArmedVisual>();
                armed.SunkDepth = SunkDepth;
                armed.Spikes = art.transform;

                if (art.GetComponentInChildren<MeshRenderer>() == null)
                {
                    Debug.LogWarning("[SpikeTileVisualInstaller] El FBX no trajo MeshRenderer: el " +
                                     "prefab queda vacío y los pinchos se siguen viendo como el piso.");
                }

                // Todo mutado ANTES de guardar: SaveAsPrefabAsset serializa lo que hay ahora, y las
                // mutaciones posteriores a la creación del asset se pierden.
                return PrefabUtility.SaveAsPrefabAsset(root, SpikePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
