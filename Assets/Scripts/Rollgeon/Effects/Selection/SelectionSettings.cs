using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Selection
{
    [Serializable]
    public class SelectionSettings
    {
        [Tooltip("Estado del slot buscado: Self, Occupied, Empty o Both.")]
        public SlotState SlotState = SlotState.Occupied;

        [Tooltip("Cuándo se resuelve la selección: antes o después de la tirada de dados.")]
        public SelectionTiming Timing = SelectionTiming.BeforeRoll;

        [ShowIf(nameof(ShowEntityFilter))]
        [Tooltip("Tipos de entidades a buscar en slots ocupados.")]
        public EntityFilterMask EntityFilter = EntityFilterMask.Enemies;

        [HideIf(nameof(IsSelf))]
        [ToggleLeft]
        [Tooltip("Si true, busca en toda la sala. Si false, usa Range desde la entidad.")]
        public bool IsGlobal;

        [ShowIf(nameof(ShowRange))]
        [MinValue(1), MaxValue(20)]
        [Tooltip("Rango BFS desde la posición del ejecutor.")]
        public int Range = 1;

        [HideIf(nameof(IsSelf))]
        [Tooltip("true → la cantidad de targets es literalmente SelectionCount. " +
                 "false → se resuelve dinámicamente via reader (TODO downstream).")]
        public bool IsConstantSelectionCount = true;

        [HideIf(nameof(IsSelf))]
        [MinValue(1), MaxValue(16)]
        [Tooltip("Cantidad de targets requeridos cuando IsConstantSelectionCount == true.")]
        public int SelectionCount = 1;

        [HideIf(nameof(IsSelf))]
        [ToggleLeft]
        [Tooltip("Elige un target random entre los válidos sin interacción del jugador.")]
        public bool AutoResolve;

        [ShowIf(nameof(ShowAutoAccept))]
        [ToggleLeft]
        [Tooltip("Auto-confirma cuando se alcanza SelectionCount.")]
        public bool AutoAccept = true;

        private bool IsSelf => SlotState == SlotState.Self;
        private bool ShowEntityFilter => SlotState == SlotState.Occupied || SlotState == SlotState.Both;
        private bool ShowRange => !IsSelf && !IsGlobal;
        private bool ShowAutoAccept => !IsSelf && !AutoResolve;

        public int GetSelectionCount(ReadInfo info)
        {
            return SelectionCount;
        }

        public bool NeedsPlayerInteraction()
        {
            return SlotState != SlotState.Self && !AutoResolve;
        }

        public bool NeedsSelectionAt(SelectionTiming t)
        {
            if (Timing != t) return false;
            return NeedsPlayerInteraction();
        }

        public List<TargetRef> ResolveValidTiles(GridCoord ownerPosition, Guid ownerGuid)
        {
            var result = new List<TargetRef>();

            Debug.Log($"[SelectionSettings] ResolveValidTiles — SlotState={SlotState} IsGlobal={IsGlobal} Range={Range} EntityFilter={EntityFilter} ownerPos={ownerPosition} ownerGuid={ownerGuid}");

            if (SlotState == SlotState.Self)
            {
                result.Add(TargetRef.At(ownerPosition));
                return result;
            }

            if (IsGlobal)
            {
                if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
                {
                    Debug.LogWarning("[SelectionSettings] IGridManager not registered");
                    return result;
                }
                int totalCoords = 0;
                foreach (var coord in grid.Graph.AllCoords())
                {
                    totalCoords++;
                    if (PassesSlotFilters(grid, coord, ownerPosition, ownerGuid))
                        result.Add(TargetRef.At(coord));
                }
                Debug.Log($"[SelectionSettings] Global scan — {totalCoords} coords checked, {result.Count} passed");
            }
            else
            {
                if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
                {
                    Debug.LogWarning("[SelectionSettings] IGridManager not registered");
                    return result;
                }
                int scanned = 0;
                foreach (var coord in grid.Graph.AllCoords())
                {
                    if (ownerPosition.Manhattan(coord) > Range) continue;
                    scanned++;
                    if (PassesSlotFilters(grid, coord, ownerPosition, ownerGuid))
                        result.Add(TargetRef.At(coord));
                }
                Debug.Log($"[SelectionSettings] Range scan — {scanned} coords within range={Range}, {result.Count} passed slot filters");
            }

            return result;
        }

        public TargetSelectionResult AutoResolveTargets(GridCoord ownerPosition, Guid ownerGuid)
        {
            var valid = ResolveValidTiles(ownerPosition, ownerGuid);
            var count = Math.Min(GetSelectionCount(default), valid.Count);

            var rng = new System.Random();
            for (int i = valid.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = valid[i];
                valid[i] = valid[j];
                valid[j] = tmp;
            }

            var selected = new List<TargetRef>();
            for (int i = 0; i < count; i++)
                selected.Add(valid[i]);

            return new TargetSelectionResult
            {
                WasCompleted = selected.Count > 0,
                SelectedTargets = selected,
            };
        }

        private bool PassesSlotFilters(IGridManager grid, GridCoord coord, GridCoord ownerPos, Guid ownerGuid)
        {
            if (coord == ownerPos) return false;

            bool isOccupied = grid.IsOccupied(coord);
            bool isFree = grid.IsFree(coord);

            switch (SlotState)
            {
                case SlotState.Occupied:
                    if (!isOccupied)
                    {
                        Debug.Log($"[SelectionSettings] PassesSlotFilters — coord={coord} REJECTED (not occupied)");
                        return false;
                    }
                    bool entityPass = PassesEntityFilter(grid, coord, ownerGuid);
                    Debug.Log($"[SelectionSettings] PassesSlotFilters — coord={coord} occupied, entityFilter={entityPass}");
                    return entityPass;

                case SlotState.Empty:
                    return isFree;

                case SlotState.Both:
                    if (isFree) return true;
                    if (isOccupied) return PassesEntityFilter(grid, coord, ownerGuid);
                    return false;

                default:
                    return false;
            }
        }

        private bool PassesEntityFilter(IGridManager grid, GridCoord coord, Guid ownerGuid)
        {
            if (EntityFilter == EntityFilterMask.None)
            {
                Debug.Log($"[SelectionSettings] PassesEntityFilter — coord={coord} REJECTED (EntityFilter is None)");
                return false;
            }

            if (!grid.TryGetOccupant(coord, out var occupantGuid) || occupantGuid == Guid.Empty)
            {
                Debug.Log($"[SelectionSettings] PassesEntityFilter — coord={coord} REJECTED (no occupant found)");
                return false;
            }

            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var entityQuery))
            {
                Debug.Log($"[SelectionSettings] PassesEntityFilter — coord={coord} ACCEPTED (no IEntityQueryService, permissive fallback)");
                return true;
            }

            var relationship = entityQuery.GetRelationship(ownerGuid, occupantGuid);
            bool passes = (EntityFilter & relationship) != 0;
            Debug.Log($"[SelectionSettings] PassesEntityFilter — coord={coord} occupant={occupantGuid} relationship={relationship} filter={EntityFilter} passes={passes}");
            return passes;
        }
    }
}
