using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Authoring;
using UnityEditor;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    /// <summary>
    /// Pure editor operations on the special-tile side of <see cref="RoomLayout"/>
    /// (permanents, slots, portal pairs). Centralized so the Special Tiles tab and
    /// tests share the same semantics — en particular la garantía de que un par de
    /// portal NUNCA queda a medio serializar: borrar un extremo borra el registro
    /// entero, mover un extremo valida la celda destino igual que un create.
    /// </summary>
    public static class SpecialTileOps
    {
        public const string UndoLabel = "Edit Special Tiles";

        // -----------------------------------------------------------------
        // Create
        // -----------------------------------------------------------------

        public static SpecialTilePlacement AddPermanent(RoomLayout layout, SpecialTileDefinitionSO def, GridCoord coord)
        {
            if (layout == null || def == null) return null;

            Undo.RecordObject(layout, UndoLabel);
            if (layout.SpecialTilePlacements == null) layout.SpecialTilePlacements = new List<SpecialTilePlacement>();

            var placement = new SpecialTilePlacement { Definition = def, Coord = coord };
            layout.SpecialTilePlacements.Add(placement);
            EditorUtility.SetDirty(layout);
            return placement;
        }

        public static SpecialTileSlot AddSlot(RoomLayout layout, GridCoord coord)
        {
            if (layout == null) return null;

            Undo.RecordObject(layout, UndoLabel);
            if (layout.SpecialTileSlots == null) layout.SpecialTileSlots = new List<SpecialTileSlot>();

            var slot = new SpecialTileSlot { SlotId = GenerateSlotId(layout), Coord = coord };
            layout.SpecialTileSlots.Add(slot);
            EditorUtility.SetDirty(layout);
            return slot;
        }

        public static PortalPairPlacement AddPortalPair(RoomLayout layout, SpecialTileDefinitionSO def, GridCoord a, GridCoord b)
        {
            if (layout == null || def == null || a == b) return null;

            Undo.RecordObject(layout, UndoLabel);
            if (layout.PortalPairs == null) layout.PortalPairs = new List<PortalPairPlacement>();

            var pair = new PortalPairPlacement { PortalDefinition = def, CoordA = a, CoordB = b };
            layout.PortalPairs.Add(pair);
            EditorUtility.SetDirty(layout);
            return pair;
        }

        // -----------------------------------------------------------------
        // Remove / Move
        // -----------------------------------------------------------------

        /// <summary>
        /// Borra la entry en <paramref name="coord"/>, sea permanente, slot o extremo de
        /// portal. Si es un extremo de portal, borra el PAR ENTERO — un portal huérfano no
        /// es un estado alcanzable desde el editor.
        /// </summary>
        public static bool RemoveAt(RoomLayout layout, GridCoord coord)
        {
            if (layout == null) return false;

            if (layout.SpecialTilePlacements != null)
            {
                int idx = layout.SpecialTilePlacements.FindIndex(p => p != null && p.Coord == coord);
                if (idx >= 0)
                {
                    Undo.RecordObject(layout, UndoLabel);
                    layout.SpecialTilePlacements.RemoveAt(idx);
                    EditorUtility.SetDirty(layout);
                    return true;
                }
            }

            if (layout.SpecialTileSlots != null)
            {
                int idx = layout.SpecialTileSlots.FindIndex(s => s != null && s.Coord == coord);
                if (idx >= 0)
                {
                    Undo.RecordObject(layout, UndoLabel);
                    layout.SpecialTileSlots.RemoveAt(idx);
                    EditorUtility.SetDirty(layout);
                    return true;
                }
            }

            if (layout.PortalPairs != null)
            {
                int idx = layout.PortalPairs.FindIndex(p => p != null && (p.CoordA == coord || p.CoordB == coord));
                if (idx >= 0)
                {
                    Undo.RecordObject(layout, UndoLabel);
                    layout.PortalPairs.RemoveAt(idx); // el par entero, nunca un extremo suelto
                    EditorUtility.SetDirty(layout);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Mueve la entry (o el extremo de portal correspondiente) validando que el destino esté libre.</summary>
        public static bool MoveTo(RoomLayout layout, GridCoord from, GridCoord to)
        {
            if (layout == null) return false;
            if (from == to) return true;
            if (!IsCellFree(layout, to)) return false;

            if (layout.SpecialTilePlacements != null)
            {
                var p = layout.SpecialTilePlacements.Find(x => x != null && x.Coord == from);
                if (p != null)
                {
                    Undo.RecordObject(layout, UndoLabel);
                    p.Coord = to;
                    EditorUtility.SetDirty(layout);
                    return true;
                }
            }

            if (layout.SpecialTileSlots != null)
            {
                var s = layout.SpecialTileSlots.Find(x => x != null && x.Coord == from);
                if (s != null)
                {
                    Undo.RecordObject(layout, UndoLabel);
                    s.Coord = to;
                    EditorUtility.SetDirty(layout);
                    return true;
                }
            }

            if (layout.PortalPairs != null)
            {
                var pair = layout.PortalPairs.Find(x => x != null && (x.CoordA == from || x.CoordB == from));
                if (pair != null)
                {
                    Undo.RecordObject(layout, UndoLabel);
                    if (pair.CoordA == from) pair.CoordA = to;
                    else pair.CoordB = to;
                    EditorUtility.SetDirty(layout);
                    return true;
                }
            }

            return false;
        }

        // -----------------------------------------------------------------
        // Queries
        // -----------------------------------------------------------------

        /// <summary>Overlap check entre las 3 listas (permanentes, slots, ambos extremos de portales).</summary>
        public static bool IsCellFree(RoomLayout layout, GridCoord coord)
        {
            if (layout == null) return true;

            if (layout.SpecialTilePlacements != null)
                foreach (var p in layout.SpecialTilePlacements)
                    if (p != null && p.Coord == coord) return false;

            if (layout.SpecialTileSlots != null)
                foreach (var s in layout.SpecialTileSlots)
                    if (s != null && s.Coord == coord) return false;

            if (layout.PortalPairs != null)
                foreach (var pp in layout.PortalPairs)
                    if (pp != null && (pp.CoordA == coord || pp.CoordB == coord)) return false;

            return true;
        }

        /// <summary>Genera un SlotId único ("SLOT_01", "SLOT_02"...), estable aunque se hayan borrado slots intermedios.</summary>
        public static string GenerateSlotId(RoomLayout layout)
        {
            var used = new HashSet<string>();
            if (layout?.SpecialTileSlots != null)
            {
                foreach (var s in layout.SpecialTileSlots)
                    if (s != null && !string.IsNullOrEmpty(s.SlotId))
                        used.Add(s.SlotId);
            }

            int n = 1;
            string id;
            do
            {
                id = $"SLOT_{n:00}";
                n++;
            } while (used.Contains(id));

            return id;
        }

        // -----------------------------------------------------------------
        // Validation
        // -----------------------------------------------------------------

        public static List<string> Validate(RoomLayout layout)
        {
            var messages = new List<string>();
            if (layout == null) return messages;

            ValidateOverlaps(layout, messages);
            ValidateSlots(layout, messages);
            ValidatePermanents(layout, messages);
            ValidatePortals(layout, messages);

            return messages;
        }

        private static void ValidateOverlaps(RoomLayout layout, List<string> messages)
        {
            var occupied = new Dictionary<GridCoord, int>();
            void Track(GridCoord c) => occupied[c] = occupied.TryGetValue(c, out var n) ? n + 1 : 1;

            if (layout.SpecialTilePlacements != null)
                foreach (var p in layout.SpecialTilePlacements) if (p != null) Track(p.Coord);
            if (layout.SpecialTileSlots != null)
                foreach (var s in layout.SpecialTileSlots) if (s != null) Track(s.Coord);
            if (layout.PortalPairs != null)
                foreach (var pp in layout.PortalPairs) if (pp != null) { Track(pp.CoordA); Track(pp.CoordB); }

            foreach (var kv in occupied)
                if (kv.Value > 1)
                    messages.Add($"ERROR: {kv.Value} entradas comparten la celda {kv.Key}.");
        }

        private static void ValidateSlots(RoomLayout layout, List<string> messages)
        {
            if (layout.SpecialTileSlots == null) return;

            var seenIds = new HashSet<string>();
            foreach (var s in layout.SpecialTileSlots)
            {
                if (s == null) continue;

                if (string.IsNullOrEmpty(s.SlotId))
                    messages.Add($"ERROR: slot en {s.Coord} sin SlotId — sin clave de roll/persistencia.");
                else if (!seenIds.Add(s.SlotId))
                    messages.Add($"ERROR: SlotId '{s.SlotId}' duplicado.");

                int nonNull = 0;
                bool hasDup = false;
                var seenOptions = new HashSet<SpecialTileDefinitionSO>();
                var effective = s.EffectiveOptions;
                if (effective != null)
                {
                    foreach (var opt in effective)
                    {
                        if (opt == null) continue;
                        nonNull++;
                        if (!seenOptions.Add(opt)) hasDup = true;
                    }
                }

                if (nonNull == 0)
                    messages.Add($"ERROR: slot '{s.SlotId}' ({s.Coord}) sin opciones efectivas.");
                if (hasDup)
                    messages.Add($"WARN: slot '{s.SlotId}' ({s.Coord}) tiene opciones duplicadas.");

                if (s.Group != null && s.Group.Options != null && s.Group.Options.Count < 2)
                    messages.Add($"WARN: el grupo '{s.Group.name}' referenciado por '{s.SlotId}' tiene menos de 2 opciones — un grupo de 1 es un permanente.");

                if (layout.NavGraph != null && !layout.NavGraph.IsEmpty && !layout.NavGraph.HasNode(s.Coord))
                    messages.Add($"WARN: slot '{s.SlotId}' en {s.Coord} — celda no caminable (¿falta rebake?).");
            }
        }

        private static void ValidatePermanents(RoomLayout layout, List<string> messages)
        {
            if (layout.SpecialTilePlacements == null) return;

            for (int i = 0; i < layout.SpecialTilePlacements.Count; i++)
            {
                var p = layout.SpecialTilePlacements[i];
                if (p == null) continue;

                if (layout.NavGraph != null && !layout.NavGraph.IsEmpty && !layout.NavGraph.HasNode(p.Coord))
                {
                    string label = p.Definition != null ? p.Definition.TileId : $"#{i}";
                    messages.Add($"WARN: permanente '{label}' en {p.Coord} — celda no caminable (¿falta rebake?).");
                }
            }
        }

        private static void ValidatePortals(RoomLayout layout, List<string> messages)
        {
            if (layout.PortalPairs == null) return;

            for (int i = 0; i < layout.PortalPairs.Count; i++)
            {
                var pp = layout.PortalPairs[i];
                if (pp == null) continue;

                if (pp.PortalDefinition == null)
                    messages.Add($"ERROR: portal #{i} sin PortalDefinition.");
                if (pp.CoordA == pp.CoordB)
                    messages.Add($"ERROR: portal #{i} con ambos extremos en {pp.CoordA}.");

                if (layout.NavGraph != null && !layout.NavGraph.IsEmpty)
                {
                    if (!layout.NavGraph.HasNode(pp.CoordA))
                        messages.Add($"WARN: portal #{i} extremo A en {pp.CoordA} — celda no caminable (¿falta rebake?).");
                    if (!layout.NavGraph.HasNode(pp.CoordB))
                        messages.Add($"WARN: portal #{i} extremo B en {pp.CoordB} — celda no caminable (¿falta rebake?).");
                }
            }
        }
    }
}
