using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Authoring;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    /// <summary>
    /// Scene-view rendering for special tiles: permanentes (quad), slots (rombo) y pares de
    /// portal (dos discos + línea punteada), más el extremo A "pendiente" cuando se está
    /// armando un par de portal a mitad de colocación.
    /// </summary>
    /// <remarks>
    /// El mapeo <see cref="GridCoord"/> → world usa <see cref="RoomLayout.TileSize"/> y
    /// <see cref="RoomLayout.GetOrigin"/> — el MISMO cálculo que <c>GridManager.CellToWorld</c>
    /// en runtime (ver <c>RoomLayout.OnDrawGizmosSelected</c> para el NavGraph, que usa la misma
    /// fórmula). Nunca el <c>_gridStep</c> del tile painter: son dos grillas independientes que
    /// no tienen por qué coincidir.
    /// </remarks>
    internal static class RoomEditorSpecialTileGizmos
    {
        private static readonly Color SelectionOutline = Color.white;
        private static readonly Color SlotColor = new Color(0.55f, 0.75f, 1f);
        private static readonly Color PendingColor = new Color(1f, 0.85f, 0.2f);

        public static void Draw(RoomLayout layout, GridCoord? pendingPortalA, Vector3? pendingMouseWorld, GridCoord? selected)
        {
            if (layout == null) return;

            if (layout.SpecialTilePlacements != null)
            {
                foreach (var p in layout.SpecialTilePlacements)
                {
                    if (p == null) continue;
                    DrawPermanent(layout, p, selected.HasValue && selected.Value == p.Coord);
                }
            }

            if (layout.SpecialTileSlots != null)
            {
                foreach (var s in layout.SpecialTileSlots)
                {
                    if (s == null) continue;
                    DrawSlot(layout, s, selected.HasValue && selected.Value == s.Coord);
                }
            }

            if (layout.PortalPairs != null)
            {
                foreach (var pp in layout.PortalPairs)
                {
                    if (pp == null) continue;
                    bool isSelected = selected.HasValue && (selected.Value == pp.CoordA || selected.Value == pp.CoordB);
                    DrawPortalPair(layout, pp, isSelected);
                }
            }

            if (pendingPortalA.HasValue)
                DrawPendingPortal(layout, pendingPortalA.Value, pendingMouseWorld);
        }

        // ============================ Shapes ============================

        private static void DrawPermanent(RoomLayout layout, SpecialTilePlacement p, bool isSelected)
        {
            var center = CellCenter(layout, p.Coord);
            var color = ColorForDefinition(p.Definition);
            float half = Mathf.Max(layout.TileSize, 0.01f) * 0.42f;

            var prev = Handles.color;
            if (isSelected)
            {
                Handles.color = SelectionOutline;
                Handles.DrawSolidRectangleWithOutline(AxisQuad(center, half * 1.2f), new Color(1f, 1f, 1f, 0.12f), SelectionOutline);
            }

            Handles.DrawSolidRectangleWithOutline(AxisQuad(center, half), new Color(color.r, color.g, color.b, 0.55f), color);
            Handles.color = prev;

            string label = p.Definition != null
                ? (string.IsNullOrEmpty(p.Definition.DisplayName) ? p.Definition.TileId : p.Definition.DisplayName)
                : "(sin definición)";
            DrawLabel(center, label, color);
        }

        private static void DrawSlot(RoomLayout layout, SpecialTileSlot s, bool isSelected)
        {
            var center = CellCenter(layout, s.Coord);
            // SpecialTileOptionGroupSO no tiene EditorColor propio — todos los slots comparten
            // un color fijo, distinto del de permanentes/portales, para diferenciarse por shape+color.
            var color = SlotColor;
            float half = Mathf.Max(layout.TileSize, 0.01f) * 0.42f;

            var prev = Handles.color;
            if (isSelected)
            {
                Handles.color = SelectionOutline;
                Handles.DrawSolidRectangleWithOutline(DiamondQuad(center, half * 1.2f), new Color(1f, 1f, 1f, 0.12f), SelectionOutline);
            }

            Handles.DrawSolidRectangleWithOutline(DiamondQuad(center, half), new Color(color.r, color.g, color.b, 0.55f), color);
            Handles.color = prev;

            int optionCount = 0;
            var effective = s.EffectiveOptions;
            if (effective != null)
                foreach (var opt in effective)
                    if (opt != null) optionCount++;

            string id = string.IsNullOrEmpty(s.SlotId) ? "(sin id)" : s.SlotId;
            DrawLabel(center, $"{id} · {optionCount} opciones", color);
        }

        private static void DrawPortalPair(RoomLayout layout, PortalPairPlacement pp, bool isSelected)
        {
            var a = CellCenter(layout, pp.CoordA);
            var b = CellCenter(layout, pp.CoordB);
            var color = ColorForDefinition(pp.PortalDefinition);
            float r = Mathf.Max(layout.TileSize, 0.01f) * 0.3f;

            var prev = Handles.color;
            Handles.color = color;
            Handles.DrawSolidDisc(a, Vector3.up, r);
            Handles.DrawSolidDisc(b, Vector3.up, r);
            Handles.DrawDottedLine(a, b, 4f);

            if (isSelected)
            {
                Handles.color = SelectionOutline;
                Handles.DrawWireDisc(a, Vector3.up, r * 1.3f);
                Handles.DrawWireDisc(b, Vector3.up, r * 1.3f);
            }
            Handles.color = prev;

            string label = pp.PortalDefinition != null
                ? (string.IsNullOrEmpty(pp.PortalDefinition.DisplayName) ? pp.PortalDefinition.TileId : pp.PortalDefinition.DisplayName)
                : "(sin definición)";
            DrawLabel((a + b) * 0.5f + Vector3.up * 0.3f, label, color);
        }

        private static void DrawPendingPortal(RoomLayout layout, GridCoord a, Vector3? mouseWorld)
        {
            var center = CellCenter(layout, a);
            // Pulso simple para que el extremo "a medio colocar" se note aunque quede quieto en pantalla.
            float pulse = 0.85f + 0.25f * Mathf.PingPong((float)EditorApplication.timeSinceStartup * 2f, 1f);
            float r = Mathf.Max(layout.TileSize, 0.01f) * 0.3f * pulse;

            var prev = Handles.color;
            Handles.color = PendingColor;
            Handles.DrawSolidDisc(center, Vector3.up, r);
            Handles.DrawWireDisc(center, Vector3.up, r * 1.3f);
            if (mouseWorld.HasValue)
                Handles.DrawDottedLine(center, mouseWorld.Value, 4f);
            Handles.color = prev;

            DrawLabel(center, "Portal A (pendiente)", PendingColor);
        }

        // ============================ Helpers ============================

        /// <summary>
        /// <see cref="GridCoord"/> → world, igual fórmula que <c>GridManager.CellToWorld</c>:
        /// <c>origin + ((X+0.5)*TileSize, height, (Y+0.5)*TileSize)</c>. La altura sale del
        /// NavGraph horneado si el nodo existe, igual que <c>RoomLayout.OnDrawGizmosSelected</c>.
        /// </summary>
        internal static Vector3 CellCenter(RoomLayout layout, GridCoord c)
        {
            var origin = layout.GetOrigin();
            float ts = Mathf.Max(layout.TileSize, 0.01f);
            float y = origin.y + 0.02f;
            if (layout.NavGraph != null && layout.NavGraph.TryGetNode(c, out var node))
                y = origin.y + node.Height + 0.02f;
            return new Vector3(origin.x + (c.X + 0.5f) * ts, y, origin.z + (c.Y + 0.5f) * ts);
        }

        /// <summary>
        /// Color de gizmo/paleta para una definición. <see cref="SpecialTileDefinitionSO.EditorColor"/>
        /// arranca en blanco por default — si el diseñador no lo tocó, derivamos un color estable
        /// del TileId para poder diferenciar tipos a simple vista sin autoría manual extra.
        /// </summary>
        internal static Color ColorForDefinition(SpecialTileDefinitionSO def)
        {
            if (def == null) return new Color(0.55f, 0.55f, 0.55f);
            if (def.EditorColor != Color.white) return def.EditorColor;
            return HashColor(string.IsNullOrEmpty(def.TileId) ? def.name : def.TileId);
        }

        private static Color HashColor(string s)
        {
            int hash = string.IsNullOrEmpty(s) ? 0 : s.GetHashCode();
            var rnd = new System.Random(hash);
            return Color.HSVToRGB((float)rnd.NextDouble(), 0.6f, 0.95f);
        }

        private static Vector3[] AxisQuad(Vector3 center, float half)
        {
            return new[]
            {
                center + new Vector3(-half, 0f, -half),
                center + new Vector3(-half, 0f, half),
                center + new Vector3(half, 0f, half),
                center + new Vector3(half, 0f, -half),
            };
        }

        private static Vector3[] DiamondQuad(Vector3 center, float half)
        {
            return new[]
            {
                center + new Vector3(0f, 0f, -half),
                center + new Vector3(-half, 0f, 0f),
                center + new Vector3(0f, 0f, half),
                center + new Vector3(half, 0f, 0f),
            };
        }

        private static void DrawLabel(Vector3 worldPos, string text, Color color)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                normal = { textColor = color },
                alignment = TextAnchor.MiddleCenter,
            };
            Handles.Label(worldPos + Vector3.up * 0.6f, text, style);
        }
    }
}
