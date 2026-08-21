using System.Linq;
using Rollgeon.Dungeon;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Asigna los íconos del floor view (fichas del mapa zoom-out) a los RoomSO por
    /// <see cref="RoomType"/> desde la hoja <c>MapChips.png</c>: Shop = MapChips_0,
    /// Enchantment = MapChips_1, Boss = MapChips_2. Idempotente — re-correrlo re-asigna.
    /// RoomSO es Odin (<c>SerializedScriptableObject</c>): SIEMPRE por acá, nunca YAML a mano.
    /// </summary>
    public static class ShellIconWirer
    {
        private const string SheetPath = "Assets/Art/UI/Minimap/MapChips.png";
        private const string RoomsFolder = "Assets/Rollgeon/Rooms";

        [MenuItem("Rollgeon/Tools/Wire Shell Icons (MapChips)")]
        public static void Wire()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
                .OfType<Sprite>()
                .ToDictionary(s => s.name, s => s);

            if (!sprites.TryGetValue("MapChips_0", out var shop)
                || !sprites.TryGetValue("MapChips_1", out var enchant)
                || !sprites.TryGetValue("MapChips_2", out var boss))
            {
                Debug.LogError($"[ShellIconWirer] '{SheetPath}' no tiene los sub-sprites " +
                               $"MapChips_0/1/2 (encontrados: {string.Join(", ", sprites.Keys)}).");
                return;
            }

            int changed = 0, skipped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:RoomSO", new[] { RoomsFolder }))
            {
                var room = AssetDatabase.LoadAssetAtPath<RoomSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (room == null) continue;

                Sprite icon = room.Type switch
                {
                    RoomType.Shop => shop,
                    RoomType.Enchantment => enchant,
                    RoomType.Boss => boss,
                    _ => null,
                };
                if (icon == null) { skipped++; continue; }
                if (room.ShellIcon == icon) { skipped++; continue; }

                room.ShellIcon = icon;
                EditorUtility.SetDirty(room);
                changed++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[ShellIconWirer] Íconos de shell asignados: {changed} salas actualizadas, " +
                      $"{skipped} sin cambio (tipo sin ficha o ya asignado).");
        }
    }
}
