using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Selection
{
    [Serializable]
    public class SelectionSettings
    {
        [Tooltip("El efecto requiere un target seleccionado antes de aplicarse.")]
        public bool RequiresSelection;

        [Tooltip("Cuándo se resuelve la selección relativa al TryExecute.")]
        public SelectionTiming Timing = SelectionTiming.BeforeResolve;

        [Tooltip("true → la cantidad de targets es literalmente SelectionCount. " +
                 "false → se resuelve dinámicamente via reader (TODO downstream).")]
        public bool IsConstantSelectionCount = true;

        [MinValue(1), MaxValue(16)]
        [Tooltip("Cantidad de targets requeridos cuando IsConstantSelectionCount == true.")]
        public int SelectionCount = 1;

        [Tooltip("Si la selección es skippeada o cancelada, el efecto no falla — se trata como no-op.")]
        public bool IsSkippable;

        [Tooltip("Sólo son válidos los slots vacíos (movimiento, teleport).")]
        public bool RequireEmptySlot;

        [Tooltip("Sólo son válidos los slots ocupados (ataques, interacciones).")]
        public bool RequireOccupiedSlot;

        [ToggleLeft]
        [Tooltip("Si true, busca en toda la sala. Si false, usa Range desde la entidad.")]
        public bool IsGlobal;

        [MinValue(1), MaxValue(20)]
        [ShowIf("@RequiresSelection && !IsGlobal")]
        [Tooltip("Rango BFS desde la posición del ejecutor.")]
        public int Range = 1;

        [ToggleLeft]
        [Tooltip("Auto-confirma cuando se alcanza SelectionCount.")]
        public bool AutoAccept = true;

        [OdinSerialize, SerializeReference]
        public BaseTargetQuery TargetQuery;

        public int GetSelectionCount(ReadInfo info)
        {
            return SelectionCount;
        }

        public bool NeedsSelectionAt(SelectionTiming t)
        {
            return RequiresSelection && Timing == t;
        }

        public List<TargetRef> ResolveValidTiles(GridCoord ownerPosition)
        {
            var result = new List<TargetRef>();

            if (IsGlobal)
            {
                if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return result;
                foreach (var coord in grid.Graph.AllCoords())
                {
                    if (!PassesSlotFilters(grid, coord, ownerPosition)) continue;
                    result.Add(TargetRef.At(coord));
                }
            }
            else
            {
                if (!ServiceLocator.TryGetService<IMovementService>(out var movement)) return result;
                foreach (var coord in movement.GetReachableTiles(ownerPosition, Range))
                    result.Add(TargetRef.At(coord));
            }

            return result;
        }

        private bool PassesSlotFilters(IGridManager grid, GridCoord coord, GridCoord ownerPos)
        {
            if (coord == ownerPos) return false;
            if (RequireEmptySlot && !grid.IsFree(coord)) return false;
            if (RequireOccupiedSlot && !grid.IsOccupied(coord)) return false;
            return true;
        }
    }
}
