using System;
using System.Collections.Generic;
using Rollgeon.Dungeon;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Una celda visible del minimapa: offset en celdas respecto de la sala actual
    /// (North = (0,+1), East = (+1,0) — misma convención que <c>DungeonManager.StepFor</c>)
    /// más el estado que decide el sprite.
    /// </summary>
    public readonly struct MinimapCell
    {
        public readonly Vector2Int Offset;
        public readonly bool IsCurrent;
        public readonly bool IsVisited;
        public readonly RoomType Type;

        public MinimapCell(Vector2Int offset, bool isCurrent, bool isVisited, RoomType type)
        {
            Offset = offset;
            IsCurrent = isCurrent;
            IsVisited = isVisited;
            Type = type;
        }
    }

    /// <summary>
    /// Modelo puro del minimapa estilo Isaac: qué celdas se muestran y con qué estado.
    /// Solo salas descubiertas (<see cref="RoomDiscovery.IsDiscovered"/>: visitadas o
    /// vecinas conectadas a una visitada) — el mismo fog of war que el floor view.
    /// </summary>
    public static class MinimapModel
    {
        /// <summary>
        /// Celdas visibles relativas a <paramref name="currentId"/>. Lista vacía si la
        /// sala actual no está en <paramref name="rooms"/> (dungeon aún no generado).
        /// </summary>
        public static List<MinimapCell> Build(
            IReadOnlyDictionary<Guid, RoomInstance> rooms, Guid currentId)
        {
            var cells = new List<MinimapCell>();
            if (rooms == null || !rooms.TryGetValue(currentId, out var current) || current == null)
                return cells;

            foreach (var (id, room) in rooms)
            {
                if (room == null) continue;
                if (!RoomDiscovery.IsDiscovered(id, rooms)) continue;

                cells.Add(new MinimapCell(
                    room.GridCell - current.GridCell,
                    isCurrent: id == currentId,
                    isVisited: room.Visited,
                    type: room.Template != null ? room.Template.Type : RoomType.Combat));
            }
            return cells;
        }
    }
}
