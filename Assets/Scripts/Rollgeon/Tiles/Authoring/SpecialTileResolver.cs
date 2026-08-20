using System.Collections.Generic;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Tiles.Authoring
{
    /// <summary>Una casilla ya resuelta, lista para <c>ISpecialTileService.Place</c>.</summary>
    public readonly struct ResolvedSpecialTile
    {
        public readonly GridCoord Coord;
        public readonly SpecialTileDefinitionSO Definition;

        /// <summary><c>perm.{i}</c> | SlotId | <c>portal.{i}.a/.b</c> — para logs/debug.</summary>
        public readonly string SourceId;

        /// <summary>Extremos de un mismo par comparten link id; −1 = no es portal.</summary>
        public readonly int PortalLinkId;

        public ResolvedSpecialTile(GridCoord coord, SpecialTileDefinitionSO definition,
            string sourceId, int portalLinkId)
        {
            Coord = coord;
            Definition = definition;
            SourceId = sourceId;
            PortalLinkId = portalLinkId;
        }
    }

    /// <summary>
    /// Resuelve la autoría de casillas de una sala a instancias concretas: permanentes
    /// pasan directo, slots rolean UNA opción autorizada (determinista por
    /// <see cref="SpecialTileSeed"/> y persistida en <see cref="SpecialTileSlotState"/> —
    /// re-entrar o resumir NUNCA re-rolea), y los pares de portal salen linkeados.
    /// </summary>
    public static class SpecialTileResolver
    {
        /// <summary>Prefijo de las keys de estado por slot en <c>RoomInstance.ObjectStates</c>.</summary>
        public const string StateKeyPrefix = "stile.";

        public static List<ResolvedSpecialTile> Resolve(RoomLayout layout, int floorSeed,
            Vector2Int roomCell, SerializableObjectStates roomStates, List<string> warnings = null)
        {
            var result = new List<ResolvedSpecialTile>();
            if (layout == null) return result;

            ResolvePermanents(layout, result, warnings);
            ResolveSlots(layout, floorSeed, roomCell, roomStates, result, warnings);
            ResolvePortalPairs(layout, result, warnings);
            return result;
        }

        private static void ResolvePermanents(RoomLayout layout,
            List<ResolvedSpecialTile> into, List<string> warnings)
        {
            if (layout.SpecialTilePlacements == null) return;
            for (int i = 0; i < layout.SpecialTilePlacements.Count; i++)
            {
                var placement = layout.SpecialTilePlacements[i];
                if (placement?.Definition == null)
                {
                    warnings?.Add($"Permanente #{i} sin definición — ignorada.");
                    continue;
                }
                into.Add(new ResolvedSpecialTile(placement.Coord, placement.Definition, $"perm.{i}", -1));
            }
        }

        private static void ResolveSlots(RoomLayout layout, int floorSeed, Vector2Int roomCell,
            SerializableObjectStates roomStates, List<ResolvedSpecialTile> into, List<string> warnings)
        {
            if (layout.SpecialTileSlots == null) return;

            foreach (var slot in layout.SpecialTileSlots)
            {
                if (slot == null) continue;
                if (string.IsNullOrEmpty(slot.SlotId))
                {
                    warnings?.Add("Slot sin SlotId — sin clave de roll/persistencia, ignorado.");
                    continue;
                }

                var options = CollectOptions(slot);
                if (options.Count == 0)
                {
                    warnings?.Add($"Slot '{slot.SlotId}' sin opciones efectivas — ignorado.");
                    continue;
                }

                string stateKey = StateKeyPrefix + slot.SlotId;

                // 1) Hidratar el estado persistido: re-entry/resume respetan la elección.
                if (roomStates != null && roomStates.TryGet<SpecialTileSlotState>(stateKey, out var saved))
                {
                    if (string.IsNullOrEmpty(saved.ChosenTileId)) continue; // resolvió vacío

                    var savedDef = FindByTileId(options, saved.ChosenTileId);
                    if (savedDef != null)
                    {
                        into.Add(new ResolvedSpecialTile(slot.Coord, savedDef, slot.SlotId, -1));
                        continue;
                    }
                    warnings?.Add($"Slot '{slot.SlotId}': la opción guardada '{saved.ChosenTileId}' " +
                                  "ya no está entre las autorizadas — se re-rolea.");
                }

                // 2) Roll determinista: mismo (floorSeed, celda, slotId) → misma elección.
                var rng = new System.Random(SpecialTileSeed.Derive(floorSeed, roomCell, slot.SlotId));
                int optionCount = options.Count + (slot.CanResolveEmpty ? 1 : 0);
                int pick = rng.Next(optionCount);
                var chosen = pick < options.Count ? options[pick] : null;

                roomStates?.Set(stateKey, new SpecialTileSlotState
                {
                    SpawnPointId = stateKey,
                    ChosenTileId = chosen != null ? chosen.TileId : string.Empty,
                });

                if (chosen != null)
                    into.Add(new ResolvedSpecialTile(slot.Coord, chosen, slot.SlotId, -1));
            }
        }

        private static void ResolvePortalPairs(RoomLayout layout,
            List<ResolvedSpecialTile> into, List<string> warnings)
        {
            if (layout.PortalPairs == null) return;
            for (int i = 0; i < layout.PortalPairs.Count; i++)
            {
                var pair = layout.PortalPairs[i];
                if (pair?.PortalDefinition == null)
                {
                    warnings?.Add($"PortalPair #{i} sin definición — ignorado.");
                    continue;
                }
                if (pair.CoordA == pair.CoordB)
                {
                    warnings?.Add($"PortalPair #{i} con ambos extremos en {pair.CoordA} — ignorado.");
                    continue;
                }

                into.Add(new ResolvedSpecialTile(pair.CoordA, pair.PortalDefinition, $"portal.{i}.a", i));
                into.Add(new ResolvedSpecialTile(pair.CoordB, pair.PortalDefinition, $"portal.{i}.b", i));
            }
        }

        private static List<SpecialTileDefinitionSO> CollectOptions(SpecialTileSlot slot)
        {
            var options = new List<SpecialTileDefinitionSO>();
            var source = slot.EffectiveOptions;
            if (source == null) return options;
            foreach (var option in source)
                if (option != null) options.Add(option);
            return options;
        }

        private static SpecialTileDefinitionSO FindByTileId(
            List<SpecialTileDefinitionSO> options, string tileId)
        {
            foreach (var option in options)
                if (option != null && option.TileId == tileId) return option;
            return null;
        }
    }
}
