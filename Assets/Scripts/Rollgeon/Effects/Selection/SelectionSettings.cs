using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Dice;
using Rollgeon.Movement;
using Rollgeon.Movement.Die;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
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

        [ShowIf(nameof(ShowMovementDie))]
        [ToggleLeft]
        [Tooltip("El rango lo define la cara del dado de Movimiento (§6.6) en vez de Range. " +
                 "Sin tirada activa usa la cara máxima del dado (rango potencial); sin " +
                 "IMovementDieService registrado cae a Range. Solo Movimiento de combate.")]
        public bool RangeFromMovementDie;

        [HideIf(nameof(IsSelf))]
        [InfoBox("AoE con SlotState.Empty expande celdas vacías; los efectos de movimiento " +
                 "solo usan el ancla.", InfoMessageType.Warning, nameof(ShowAoeMovementWarning))]
        [Tooltip("Single: N picks individuales (SelectionCount). " +
                 "Aoe: se elige UNA celda ancla y el efecto se expande alrededor.")]
        public TargetMode TargetMode = TargetMode.Single;

        [ShowIf(nameof(IsAoe))]
        [Tooltip("Radius: diamante Manhattan alrededor del ancla. Custom: patrón bool-grid " +
                 "relativo al ancla.")]
        public AoeShape AoeShape = AoeShape.Radius;

        [ShowIf(nameof(ShowAoeRadius))]
        [MinValue(1), MaxValue(10)]
        [Tooltip("Celdas a distancia Manhattan <= radio del ancla. El área se clipea a la " +
                 "grilla, NO al Range del caster, y re-aplica SlotState + EntityFilter.")]
        public int AoeRadius = 1;

        [ShowIf(nameof(ShowAoePattern))]
        [MinValue(1), MaxValue(11)]
        public int PatternRows = 1;

        [ShowIf(nameof(ShowAoePattern))]
        [MinValue(1), MaxValue(11)]
        public int PatternCols = 1;

        [ShowIf(nameof(ShowAoePattern))]
        [Tooltip("Celda del patrón que se apoya sobre el ancla: x = columna (+X), y = fila (+Y).")]
        public Vector2Int PatternCenter = new Vector2Int(0, 0);

        [ShowIf(nameof(ShowAoePattern))]
        [BoolGrid(nameof(PatternRows), nameof(PatternCols), nameof(PatternCenter))]
        public bool[] PatternFlat = { true };

        [ShowIf(nameof(ShowCount))]
        [Tooltip("true → la cantidad de targets es literalmente SelectionCount. " +
                 "false → se resuelve dinámicamente via SelectionCountReader.")]
        public bool IsConstantSelectionCount = true;

        [ShowIf(nameof(ShowConstantCount))]
        [MinValue(1), MaxValue(16)]
        [Tooltip("Cantidad de targets requeridos cuando IsConstantSelectionCount == true.")]
        public int SelectionCount = 1;

        [ShowIf(nameof(ShowDynamicCount))]
        [OdinSerialize, SerializeReference]
        [Tooltip("Reader polimórfico que resuelve la cantidad de targets en runtime. Null => 1.")]
        public ISelectionCountReader SelectionCountReader;

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
        private bool ShowMovementDie => ShowRange && RangeMode == RangeMode.PathReachable;
        private bool ShowAutoAccept => !IsSelf && !AutoResolve;
        private bool IsAoe => !IsSelf && TargetMode == TargetMode.Aoe;
        private bool ShowAoeRadius => IsAoe && AoeShape == AoeShape.Radius;
        private bool ShowAoePattern => IsAoe && AoeShape == AoeShape.Custom;
        private bool ShowAoeMovementWarning => IsAoe && SlotState == SlotState.Empty;
        // En AoE el count no aplica: siempre se elige 1 ancla y el área define el resto.
        private bool ShowCount => !IsSelf && !IsAoe;
        private bool ShowConstantCount => ShowCount && IsConstantSelectionCount;
        private bool ShowDynamicCount => ShowCount && !IsConstantSelectionCount;

        /// <summary>
        /// True si la selección apunta a enemigos (ataque / habilidad de clase). Estos
        /// pintan TODO el rango geométrico con el tinte base "range" y los slots
        /// seleccionables (con enemigo) con "attack" por encima. Centraliza la condición
        /// antes duplicada inline en el hover preview y en el chain de combate.
        /// </summary>
        public bool TargetsEnemies => SlotState != SlotState.Empty
                                      && SlotState != SlotState.Self
                                      && (EntityFilter & EntityFilterMask.Enemies) != 0;

        /// <summary>
        /// Cantidad de PICKS que debe hacer el jugador (no de targets finales): en AoE
        /// siempre 1 — el ancla — y la expansión del área ocurre después, en
        /// <see cref="ExpandAoe"/>. Con count dinámico, un reader null cae a 1.
        /// </summary>
        public int GetSelectionCount(ReadInfo info)
        {
            if (IsAoe) return 1;
            if (IsConstantSelectionCount) return SelectionCount;
            return SelectionCountReader?.Read(info) ?? 1;
        }

        public bool NeedsPlayerInteraction()
        {
            return SlotState != SlotState.Self && !AutoResolve;
        }

        /// <summary>
        /// Rango efectivo del owner (§6.6). Con <see cref="RangeFromMovementDie"/>: la cara
        /// revelada del dado de Movimiento si hay tirada vigente; si no, la cara máxima del
        /// dado — el rango POTENCIAL, para que el gate del botón, el hover preview y el drag
        /// pre-tirada sigan mostrando "hay algo alcanzable". En ambas ramas se suma el bonus
        /// del stat <c>MoveRange</c> (reward "Movimiento+", BUG-85) para que gate, preview y
        /// rango real queden coherentes. Sin servicio registrado (tests, exploración sin
        /// wiring) o sin el flag, el <see cref="Range"/> autorado.
        /// </summary>
        public int ResolveEffectiveRange(Guid ownerGuid)
        {
            if (!RangeFromMovementDie) return Range;
            if (!ServiceLocator.TryGetService<IMovementDieService>(out var die) || die == null)
                return Range;
            // Piso 1: un malus de MoveRange (Guantelete Pesado) nunca deja al jugador
            // sin poder moverse (GDD: "la velocidad no baja del mínimo").
            int bonus = ResolveMoveRangeBonus(ownerGuid);
            if (die.TryGetActiveRange(ownerGuid, out var rolled)) return Math.Max(1, rolled + bonus);
            return Math.Max(1, die.CurrentType.MaxFace() + bonus);
        }

        // Bonus de MoveRange del owner; degrada a 0 sin AttributesManager o si la
        // entidad no tiene el stat (enemigos, tests) — GetAttributeModifiedValue
        // devuelve default sin loguear en ese caso.
        private static int ResolveMoveRangeBonus(Guid ownerGuid)
        {
            if (ownerGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<Rollgeon.Attributes.AttributesManager>(out var attrs)
                || attrs == null)
                return 0;
            return attrs.GetAttributeModifiedValue<Rollgeon.Attributes.Stats.MoveRange, int>(ownerGuid);
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
                    foreach (var coord in movement.GetReachableTiles(ownerPosition, ResolveEffectiveRange(ownerGuid)))
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
                        if (ownerPosition.Manhattan(coord) > ResolveEffectiveRange(ownerGuid)) continue;
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
                foreach (var coord in movement.GetReachableTiles(ownerPosition, ResolveEffectiveRange(ownerGuid)))
                    if (coord != ownerPosition) result.Add(coord);
                return result;
            }

            if (RangeMode == RangeMode.PathReachable)
                Debug.LogWarning("[SelectionSettings] IMovementService not registered — " +
                                 "fallback a rango Manhattan");

            foreach (var coord in grid.Graph.AllCoords())
            {
                if (coord == ownerPosition) continue;
                if (ownerPosition.Manhattan(coord) > ResolveEffectiveRange(ownerGuid)) continue;
                result.Add(coord);
            }
            return result;
        }

        /// <summary>
        /// Expande el ancla AoE al set final de targets. El área se clipea a la grilla
        /// (InBounds), NO al Range del caster — una explosión en el borde del alcance
        /// derrama más allá. Cada celda expandida re-aplica <see cref="PassesSlotFilters"/>
        /// (SlotState + EntityFilter, sin constraint de rango); el ancla entra siempre
        /// (ya salió de <see cref="ResolveValidTiles"/>). En Single devuelve solo el ancla.
        /// </summary>
        public List<TargetRef> ExpandAoe(GridCoord anchor, Guid ownerGuid)
        {
            var result = new List<TargetRef> { TargetRef.At(anchor) };

            if (!IsAoe) return result;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogWarning("[SelectionSettings] IGridManager not registered");
                return result;
            }

            // Sin posición del owner (tests/exploración) se usa un sentinel fuera de
            // grilla: PassesSlotFilters excluye la celda del owner y (0,0) no debe
            // quedar excluida por accidente.
            if (!grid.TryGetPosition(ownerGuid, out var ownerPos))
                ownerPos = new GridCoord(int.MinValue, int.MinValue);

            foreach (var coord in EnumerateAoeArea(anchor))
            {
                if (coord == anchor) continue;
                if (!grid.InBounds(coord)) continue;
                if (!PassesSlotFilters(grid, coord, ownerPos, ownerGuid)) continue;
                result.Add(TargetRef.At(coord));
            }

            return result;
        }

        private IEnumerable<GridCoord> EnumerateAoeArea(GridCoord anchor)
        {
            if (AoeShape == AoeShape.Radius)
            {
                for (int dx = -AoeRadius; dx <= AoeRadius; dx++)
                for (int dy = -AoeRadius; dy <= AoeRadius; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > AoeRadius) continue;
                    yield return new GridCoord(anchor.X + dx, anchor.Y + dy);
                }
                yield break;
            }

            // Custom: patrón bool-grid relativo al ancla (port de PatternUtil de Bot-Game,
            // sin flipY — acá no hay espejo por jugador — ni modo Absolute). Guard de
            // índice por si PatternFlat quedó desincronizado con Rows/Cols (el drawer
            // auto-resizea recién al dibujarse).
            if (PatternFlat == null || PatternFlat.Length == 0) yield break;

            for (int r = 0; r < PatternRows; r++)
            for (int c = 0; c < PatternCols; c++)
            {
                int idx = r * PatternCols + c;
                if (idx >= PatternFlat.Length || !PatternFlat[idx]) continue;
                yield return new GridCoord(
                    anchor.X + (c - PatternCenter.x),
                    anchor.Y + (r - PatternCenter.y));
            }
        }

        public TargetSelectionResult AutoResolveTargets(GridCoord ownerPosition, Guid ownerGuid)
        {
            var valid = ResolveValidTiles(ownerPosition, ownerGuid);

            if (IsAoe)
            {
                if (valid.Count == 0)
                {
                    return new TargetSelectionResult
                    {
                        WasCompleted = false,
                        SelectedTargets = new List<TargetRef>(),
                    };
                }

                var anchor = valid[new System.Random().Next(valid.Count)].Coord;
                var expanded = ExpandAoe(anchor, ownerGuid);
                return new TargetSelectionResult
                {
                    WasCompleted = expanded.Count > 0,
                    SelectedTargets = expanded,
                };
            }

            var count = Math.Min(GetSelectionCount(new ReadInfo { ownerGuid = ownerGuid }), valid.Count);

            var rng = new System.Random();
            for (int i = valid.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = valid[i];
                valid[i] = valid[j];
                valid[j] = tmp;
            }

            // Dedupe por ocupante (Fase C): dos celdas del mismo footprint multi-celda son
            // el mismo target — el auto-resolve no puede elegirlo dos veces.
            ServiceLocator.TryGetService<IGridManager>(out var pickGrid);
            var pickedOccupants = new HashSet<Guid>();
            var selected = new List<TargetRef>();
            for (int i = 0; i < valid.Count && selected.Count < count; i++)
            {
                if (pickGrid != null
                    && pickGrid.TryGetOccupant(valid[i].Coord, out var occupant)
                    && occupant != Guid.Empty
                    && !pickedOccupants.Add(occupant))
                {
                    continue;
                }
                selected.Add(valid[i]);
            }

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
