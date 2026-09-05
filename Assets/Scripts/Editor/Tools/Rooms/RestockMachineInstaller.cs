using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.Shop;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Arma la máquina de reroll de la tienda (§17.F.5) y el layout sandwich pedido
    /// por diseño:
    /// <code>
    ///   ITEM  ITEM  ITEM      (fila z=-3)
    ///        RULETA           (centro, celda 0,0)
    ///   ITEM  ITEM  ITEM      (fila z=3)
    /// </code>
    /// 3 tiles de aire entre la ruleta y cada fila: parado al lado de la máquina no
    /// dispara ningún prompt de compra (rango 1.5 al borde del visual) y viceversa.
    /// Celdas verificadas caminables en los NavGraphs de las 3 tiendas.
    /// </summary>
    /// <remarks>
    /// 1) Construye <c>PF_RestockMachine.prefab</c> desde <c>Ruletav01.fbx</c>.
    /// 2) Reposiciona los 6 RewardSpawnPoints + crea <c>RestockMachinePoint</c> en
    ///    los 3 prefabs de tienda y cablea <c>RoomLayout.RestockMachinePoint</c>.
    /// 3) Setea <c>ShopConfigSO</c>: prefab + AllowRestock. Idempotente.
    /// </remarks>
    public static class RestockMachineInstaller
    {
        private const string ModelPath = "Assets/Art/3D/Models/Props/RouletteShop.fbx";
        private const string PrefabPath = "Assets/Prefabs/Props/PF_RestockMachine.prefab";
        private const string ShopConfigPath = "Assets/Rollgeon/Rooms/Shop/ShopConfig.asset";

        private static readonly string[] ShopPrefabPaths =
        {
            "Assets/Prefabs/Rooms/FloorOne/Shop_Room01.prefab",
            "Assets/Prefabs/Rooms/FloorTwo/Shop_Room_FloorTwo01.prefab",
            "Assets/Prefabs/Rooms/FloorThree/Shop_Room_FloorThree.prefab",
        };

        // Mundo = celda + 0.5. Filas en celdas z=-3 / z=3; el pedestal del medio queda
        // en x=0.5 y los laterales van 1 tile más afuera (celdas ±3) SOLO si ambas
        // celdas existen en el NavGraph bakeado — en las tiendas de piso 2/3 las
        // esquinas tienen deco y la fila queda compacta (celdas ±2) para no apoyar
        // un pedestal sobre una celda inexistente. Ruleta en (0,0), apoyada en Y=0.5.
        private const int FrontRowCellZ = -3;
        private const int BackRowCellZ = 3;
        private const float ItemY = 0.5f;
        private static readonly Vector3 MachineLocalPos = new Vector3(0.5f, 0.5f, 0.5f);

        [MenuItem("Rollgeon/Shop/Build Restock Machine")]
        public static void Build()
        {
            var machinePrefab = BuildMachinePrefab();
            if (machinePrefab == null) return;

            foreach (var path in ShopPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (ApplySandwichLayout(root, path))
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(ShopConfigPath);
            if (config == null)
            {
                Debug.LogError($"[RestockMachine] No hay ShopConfigSO en {ShopConfigPath}.");
            }
            else
            {
                config.RestockMachinePrefab = machinePrefab;
                config.AllowRestock = true;
                // El costo escala por uso (base × mult^usos) — el freno es económico,
                // como en Isaac; usos infinitos salvo que diseño lo capee después.
                config.MaxRestocks = 0;
                EditorUtility.SetDirty(config);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[RestockMachine] Listo: prefab + layout sandwich + config cableados.");
        }

        private static GameObject BuildMachinePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"[RestockMachine] No se pudo cargar el modelo en {ModelPath}.");
                return null;
            }

            var root = new GameObject("PF_RestockMachine");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual";
                visual.transform.SetParent(root.transform, worldPositionStays: false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;

                // Apoyar la base del modelo en el piso (los FBX de props traen el pivot
                // en el centro — mismo criterio que RoomPropScatter.LiftToFloor).
                var renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    visual.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
                }

                // Collider ≤ tile (regla de colliders 04/09: reach XZ ≤ ~0.5 o roba los
                // clicks de las celdas vecinas), alto para que el cursor lo acuse.
                var collider = root.AddComponent<BoxCollider>();
                float height = renderers.Length > 0
                    ? Mathf.Max(1f, renderers[0].bounds.size.y)
                    : 1.5f;
                collider.size = new Vector3(0.95f, height, 0.95f);
                collider.center = new Vector3(0f, height * 0.5f, 0f);

                var interactable = root.AddComponent<RestockMachineInteractable>();
                var so = new SerializedObject(interactable);
                so.FindProperty("_spinRoot").objectReferenceValue = visual.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[RestockMachine] Prefab guardado en {PrefabPath}.");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static bool ApplySandwichLayout(GameObject root, string path)
        {
            var layout = root.GetComponent<RoomLayout>();
            if (layout == null)
            {
                Debug.LogWarning($"[RestockMachine] {path}: sin RoomLayout — salteado.");
                return false;
            }

            var points = new List<Transform>();
            foreach (var t in layout.RewardSpawnPoints)
                if (t != null) points.Add(t);
            if (points.Count < 6)
            {
                Debug.LogWarning($"[RestockMachine] {path}: {points.Count} RewardSpawnPoints " +
                                 "(<6) — correr antes 'Expand Shop Pedestals To Six'.");
                return false;
            }

            // Item1-3 = fila delantera, Item4-6 = fila trasera (orden del expander).
            PlaceRow(layout, path, points[0], points[1], points[2], FrontRowCellZ);
            PlaceRow(layout, path, points[3], points[4], points[5], BackRowCellZ);

            var machinePoint = layout.RestockMachinePoint;
            if (machinePoint == null)
            {
                var parent = points[0].parent != null ? points[0].parent : root.transform;
                var go = new GameObject("RestockMachinePoint");
                go.transform.SetParent(parent, worldPositionStays: false);
                machinePoint = go.transform;
                layout.RestockMachinePoint = machinePoint;
            }
            machinePoint.localPosition = MachineLocalPos;
            machinePoint.localRotation = Quaternion.identity;

            Debug.Log($"[RestockMachine] {path}: sandwich aplicado (filas z={FrontRowCellZ}/{BackRowCellZ}, " +
                      $"ruleta en {MachineLocalPos}).");
            return true;
        }

        // Los laterales solo se abren a las celdas ±3 si AMBAS existen en el NavGraph
        // bakeado; si una falta (deco en la esquina), la fila entera queda en ±2 para
        // mantener la simetría.
        private static void PlaceRow(RoomLayout layout, string path,
            Transform left, Transform mid, Transform right, int cellZ)
        {
            bool spread = HasCell(layout, -3, cellZ) && HasCell(layout, 3, cellZ);
            if (!spread)
                Debug.Log($"[RestockMachine] {path}: fila z={cellZ} compacta (celda ±3 " +
                          "no existe en el NavGraph — deco en la esquina).");

            float rowZ = cellZ + 0.5f;
            left.localPosition = new Vector3(spread ? -2.5f : -1.5f, ItemY, rowZ);
            mid.localPosition = new Vector3(0.5f, ItemY, rowZ);
            right.localPosition = new Vector3(spread ? 3.5f : 2.5f, ItemY, rowZ);
        }

        private static bool HasCell(RoomLayout layout, int x, int z)
            => layout.NavGraph != null
               && layout.NavGraph.HasNode(new Rollgeon.Grid.GridCoord(x, z));
    }
}
