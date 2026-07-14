using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Movement;
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
        [Tooltip("Rango desde la posición del ejecutor (interpretado según RangeMode).")]
        public int Range = 1;

        [ShowIf(nameof(ShowRange))]
        [Tooltip("Manhattan: distancia pura, ignora paredes (ataques). " +
                 "PathReachable: BFS real por celdas caminables no ocupadas (movimiento).")]
        public RangeMode RangeMode = RangeMode.Manhattan;

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

        /// <summary>
        /// True si la selección apunta a enemigos (ataque / ataque especial). Estos
        /// pintan TODO el rango geométrico con el tinte base "range" y los slots
        /// seleccionables (con enemigo) con "attack" por encima. Centraliza la condición
        /// antes duplicada inline en el hover preview y en el chain de combate.
        /// </summary>
        public bool TargetsEnemies => SlotState != SlotState.Empty
                                      && SlotState != SlotState.Self
                                      && (EntityFilter & EntityFilterMask.Enemies) != 0;

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
                foreach (var coord in grid.Graph.AllCoords())
                {
                    if (PassesSlotFilters(grid, coord, ownerPosition, ownerGuid))
                        result.Add(TargetRef.At(coord));
                }
            }
            else
            {
                if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
                {
                    Debug.LogWarning("[SelectionSettings] IGridManager not registered");
                    return result;
                }

                if (RangeMode == RangeMode.PathReachable
                    && ServiceLocator.TryGetService<IMovementService>(out var movement))
                {
                    foreach (var coord in movement.GetReachableTiles(ownerPosition, Range))
                    {
                        if (PassesSlotFilters(grid, coord, ownerPosition, ownerGuid))
                            result.Add(TargetRef.At(coord));
                    }
                }
                else
                {
                    if (RangeMode == RangeMode.PathReachable)
                        Debug.LogWarning("[SelectionSettings] IMovementService not registered — " +
                                         "fallback a rango Manhattan");

                    foreach (var coord in grid.Graph.AllCoords())
                    {
                        if (ownerPosition.Manhattan(coord) > Range) continue;
                        if (PassesSlotFilters(grid, coord, ownerPosition, ownerGuid))
                            result.Add(TargetRef.At(coord));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Footprint geométrico completo del alcance: mismas casillas que recorre
        /// <see cref="ResolveValidTiles"/> pero SIN aplicar <see cref="PassesSlotFilters"/> —
        /// solo excluye la casilla del owner. Se usa para pintar TODO el rango de un
        /// ataque (tinte "range") con los targets seleccionables por encima; no altera qué
        /// casillas son clickeables (eso lo sigue definiendo <see cref="ResolveValidTiles"/>).
        /// </summary>
        public List<GridCoord> ResolveRangeTiles(GridCoord ownerPosition, Guid ownerGuid)
        {
            var result = new List<GridCoord>();

            if (SlotState == SlotState.Self)
                return result;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogWarning("[SelectionSettings] IGridManager not registered");
                return result;
            }

            if (IsGlobal)
            {
                foreach (var coord in grid.Graph.AllCoords())
                    if (coord != ownerPosition) result.Add(coord);
                return result;
            }

            if (RangeMode == RangeMode.PathReachable
                && ServiceLocator.TryGetService<IMovementService>(out var movement))
            {
                foreach (var coord in movement.GetReachableTiles(ownerPosition, Range))
                    if (coord != ownerPosition) result.Add(coord);
                return result;
            }

            if (RangeMode == RangeMode.PathReachable)
                Debug.LogWarning("[SelectionSettings] IMovementService not registered — " +
                                 "fallback a rango Manhattan");

            foreach (var coord in grid.Graph.AllCoords())
            {
                if (coord == ownerPosition) continue;
                if (ownerPosition.Manhattan(coord) > Range) continue;
                result.Add(coord);
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
                    if (!isOccupied) return false;
                    return PassesEntityFilter(grid, coord, ownerGuid);

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
            if (EntityFilter == EntityFilterMask.None) return false;

            if (!grid.TryGetOccupant(coord, out var occupantGuid) || occupantGuid == Guid.Empty)
                return false;

            // Sin IEntityQueryService (tests/bootstrap temprano) se acepta cualquier
            // occupant para no romper flujos que no registran el servicio.
            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var entityQuery))
                return true;

            var relationship = entityQuery.GetRelationship(ownerGuid, occupantGuid);
            return (EntityFilter & relationship) != 0;
        }
    }
}
