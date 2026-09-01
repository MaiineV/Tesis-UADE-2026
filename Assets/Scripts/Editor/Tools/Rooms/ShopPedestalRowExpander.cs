using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Rollgeon.Shop;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Expande las tiendas de 3 a 6 slots: agrega una segunda fila de spawn points
    /// (<c>Item4..6</c>, misma x que la fila original, z +3) a cada prefab de
    /// Shop room y sube <see cref="ShopConfigSO.MaxItemSlots"/> a 6. Idempotente:
    /// una sala que ya tiene 6+ RewardSpawnPoints se saltea. El config del
    /// tutorial (SC_Tutorial, 1 slot) no se toca.
    /// </summary>
    /// <remarks>
    /// Los pedestales son runtime-spawned (ShopManagerService) y bloquean su celda
    /// vía <c>PropTileBlocker</c> dinámico — no hace falta rebakear el NavGraph.
    /// La validación de abajo solo avisa si una celda nueva no es nodo caminable
    /// del graph baked (pedestal en pared / fuera del piso).
    /// </remarks>
    public static class ShopPedestalRowExpander
    {
        private const int TargetSlots = 6;
        // z+2 (una celda de pasillo entre filas): la fila z+3 caía en celdas con
        // props ((3,3) en las 3 tiendas, (-3,3) en FloorThree) fuera del NavGraph;
        // en Y=2 las mismas x de la fila original (-3, 0, 3) son caminables en las 3.
        private const float RowZOffset = 2f;
        private const string ShopConfigPath = "Assets/Rollgeon/Rooms/Shop/ShopConfig.asset";

        private static readonly string[] ShopPrefabPaths =
        {
            "Assets/Prefabs/Rooms/FloorOne/Shop_Room01.prefab",
            "Assets/Prefabs/Rooms/FloorTwo/Shop_Room_FloorTwo01.prefab",
            "Assets/Prefabs/Rooms/FloorThree/Shop_Room_FloorThree.prefab",
        };

        [MenuItem("Rollgeon/Rooms/Expand Shop Pedestals To Six")]
        public static void Expand()
        {
            var findings = new List<string>();
            int touched = 0;

            foreach (var path in ShopPrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (ExpandRoom(root, path, findings))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        touched++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var config = AssetDatabase.LoadAssetAtPath<ShopConfigSO>(ShopConfigPath);
            if (config == null)
            {
                findings.Add($"{ShopConfigPath}: no encontrado — MaxItemSlots queda como estaba.");
            }
            else if (config.MaxItemSlots != TargetSlots)
            {
                config.MaxItemSlots = TargetSlots;
                EditorUtility.SetDirty(config);
                Debug.Log($"[ShopPedestalRowExpander] {ShopConfigPath}: MaxItemSlots → {TargetSlots}.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShopPedestalRowExpander] Listo — {touched} prefab(s) de shop expandidos.");
            if (findings.Count > 0)
                Debug.LogWarning("[ShopPedestalRowExpander] Findings:\n• " + string.Join("\n• ", findings));
        }

        private static bool ExpandRoom(GameObject root, string path, List<string> findings)
        {
            var layout = root.GetComponent<RoomLayout>();
            if (layout == null)
            {
                findings.Add($"{path}: sin RoomLayout — salteado.");
                return false;
            }

            var existing = new List<Transform>();
            foreach (var t in layout.RewardSpawnPoints)
                if (t != null) existing.Add(t);

            if (existing.Count == 0)
            {
                findings.Add($"{path}: sin RewardSpawnPoints de referencia — salteado.");
                return false;
            }

            var parent = existing[0].parent != null ? existing[0].parent : root.transform;
            int baseCount = Mathf.Min(existing.Count, TargetSlots / 2);

            // Re-runnable como repair: si la fila trasera ya existe, se reposiciona
            // (una corrida previa pudo dejarla en celdas inválidas) en vez de duplicar.
            for (int i = 0; i < baseCount && baseCount + i < TargetSlots; i++)
            {
                var reference = existing[i];
                int slotIndex = baseCount + i;

                Transform point;
                if (slotIndex < existing.Count)
                {
                    point = existing[slotIndex];
                }
                else
                {
                    var go = new GameObject($"Item{slotIndex + 1}");
                    go.transform.SetParent(parent, worldPositionStays: false);
                    layout.RewardSpawnPoints.Add(go.transform);
                    point = go.transform;
                }

                point.localPosition = reference.localPosition + new Vector3(0f, 0f, RowZOffset);
                point.localRotation = reference.localRotation;
                ValidateWalkable(layout, point, path, findings);
            }

            Debug.Log($"[ShopPedestalRowExpander] {path}: RewardSpawnPoints " +
                      $"{existing.Count} → {layout.RewardSpawnPoints.Count} (fila trasera en z+{RowZOffset}).");
            return true;
        }

        // Aviso (no bloqueo): la celda de un spawn nuevo debería ser nodo caminable
        // del NavGraph baked; si no lo es, el pedestal quedaría en pared/fuera del piso.
        private static void ValidateWalkable(RoomLayout layout, Transform point, string path, List<string> findings)
        {
            if (layout.NavGraph == null || layout.NavGraph.IsEmpty) return;

            var origin = layout.GridOrigin != null ? layout.GridOrigin.position : layout.transform.position;
            float ts = Mathf.Max(layout.TileSize, 0.01f);
            var local = point.position - origin;
            var coord = new GridCoord(Mathf.FloorToInt(local.x / ts), Mathf.FloorToInt(local.z / ts));

            foreach (var node in layout.NavGraph.Nodes)
                if (node.Coord.Equals(coord)) return;

            findings.Add($"{path}: '{point.name}' en celda {coord} no es nodo del NavGraph — " +
                         "revisar posición a mano (¿pared/fuera del piso?).");
        }
    }
}
