using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.AI.Pathing
{
    /// <summary>
    /// Implementación de <see cref="IAIPathPlanner"/>: búsqueda de etiquetas
    /// (label-correcting Dijkstra) sobre el estado <c>(celda, pasos, HP proyectado)</c> con
    /// dominancia de Pareto. Sin casillas especiales en la sala, delega en una réplica
    /// exacta del scoring legacy de <c>AINode_Move</c>/<c>AINode_KeepDistance</c> — compat
    /// bit a bit con los árboles de IA existentes.
    /// </summary>
    /// <remarks>
    /// El costo IA es interno: el movimiento real siempre cuesta 1 por celda y
    /// <see cref="AIPathRequest.MaxSteps"/> es el único límite físico. Las fórmulas del GDD
    /// viven en helpers estáticos puros (<see cref="ComputeHazardPenalty"/>,
    /// <see cref="ComputeTileCost"/>, <see cref="ComputeTerrainModifier"/>,
    /// <see cref="ComputeTacticalGainFinal"/>) para testearse con valores exactos.
    /// </remarks>
    public sealed class AIPathPlanner : IAIPathPlanner
    {
        private readonly IGridManager _grid;
        private readonly ISpecialTileAIQuery _tiles;
        private readonly AIPathTuningSO _tuning;

        /// <summary>Deps explícitas para tests; con null se resuelven frescas del ServiceLocator
        /// en cada plan (el grid es run-scoped y este planner puede ser global).</summary>
        public AIPathPlanner(IGridManager grid = null, ISpecialTileAIQuery tiles = null, AIPathTuningSO tuning = null)
        {
            _grid = grid;
            _tiles = tiles;
            _tuning = tuning;
        }

        private IGridManager ResolveGrid()
            => _grid ?? (ServiceLocator.TryGetService<IGridManager>(out var g) ? g : null);

        private ISpecialTileAIQuery ResolveTiles()
            => _tiles ?? (ServiceLocator.TryGetService<ISpecialTileAIQuery>(out var t) ? t : null);

        private int BandWeight => _tuning != null ? _tuning.BandWeight : 3;
        private float HealMaxHpPct => _tuning != null ? _tuning.HealMaxHpPct : 0.6f;
        private int HealDetourMaxTiles => _tuning != null ? _tuning.HealDetourMaxTiles : 2;
        private int HealBenefitScale => _tuning != null ? _tuning.HealBenefitScale : 4;
        private int FortressBenefit => _tuning != null ? _tuning.FortressBenefit : 2;
        private int SafeZoneBenefit => _tuning != null ? _tuning.SafeZoneBenefit : 3;

        // ======================================================================
        // IAIPathPlanner
        // ======================================================================

        /// <inheritdoc />
        public AIPathPlanResult PlanMove(in AIPathRequest request)
        {
            var grid = ResolveGrid();
            if (grid == null || request.MaxSteps <= 0) return AIPathPlanResult.NoMove;

            // Multi-celda (Fase B): planea footprint-aware pero CIEGO a hazards aunque haya
            // casillas — LabelPlan razona celda por celda y la regla de activación de una
            // casilla bajo un rectángulo es decisión de Fase C. La física real (path filter)
            // sigue resolviendo por el ancla.
            if (!GridFootprint.IsUnit(grid.GetFootprint(request.SelfGuid)))
                return LegacyPlan(grid, request);

            var tiles = ResolveTiles();
            if (tiles == null || !tiles.HasAnySpecialTiles)
                return LegacyPlan(grid, request);

            return LabelPlan(grid, tiles, request);
        }

        // ======================================================================
        // Fast path — réplica exacta del scoring legacy (sala sin casillas)
        // ======================================================================

        private static AIPathPlanResult LegacyPlan(IGridManager grid, in AIPathRequest r)
        {
            // Footprint del self (Fase B): las distancias son rect-a-target (celda más
            // cercana) y los candidatos son ANCLAS donde el rectángulo entero cabe. Para un
            // 1×1 la matemática y el orden son idénticos al scoring legacy.
            var fp = grid.GetFootprint(r.SelfGuid);
            var reachable = ReachableTiles(grid, r.SelfGuid, fp, r.Origin, r.MaxSteps);
            int currentDist = GridFootprint.ManhattanDistance(r.Origin, fp, r.TargetCoord);
            var best = r.Origin;

            if (r.Intent == MoveIntent.Approach)
            {
                // AINode_Move: minimizar |dist − desired|, '<' estricto (empate ⇒ no mover).
                int bestErr = Mathf.Abs(currentDist - r.DesiredRange);
                foreach (var candidate in reachable)
                {
                    int err = Mathf.Abs(GridFootprint.ManhattanDistance(candidate, fp, r.TargetCoord) - r.DesiredRange);
                    if (err < bestErr) { bestErr = err; best = candidate; }
                }
            }
            else
            {
                // AINode_KeepDistance: maximizar min(dist, ideal), '>' estricto.
                int bestScore = currentDist;
                foreach (var candidate in reachable)
                {
                    int dist = Mathf.Min(GridFootprint.ManhattanDistance(candidate, fp, r.TargetCoord), r.DesiredRange);
                    if (dist <= bestScore) continue;
                    bestScore = dist;
                    best = candidate;
                }
            }

            if (best == r.Origin) return AIPathPlanResult.NoMove;
            // Path null: el ejecutor usa el Move clásico, con el mismo A* y los mismos eventos.
            return new AIPathPlanResult(true, best, null);
        }

        /// <summary>BFS calcado de <c>MovementService.GetReachableTiles</c> — el ORDEN de
        /// descubrimiento importa: con scoring estricto, el primero entre iguales gana.
        /// El filtro es <c>CanPlace(ancla, fp, ignore: self)</c>: para 1×1 equivale al
        /// <c>IsWalkable && !IsOccupied</c> de siempre (la celda propia ya está en visited).</summary>
        private static List<GridCoord> ReachableTiles(IGridManager grid, Guid selfGuid, Vector2Int fp,
            GridCoord origin, int range)
        {
            var result = new List<GridCoord>();
            if (range < 0) return result;

            var visited = new Dictionary<GridCoord, int> { [origin] = 0 };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(origin);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int distance = visited[current];

                if (distance > 0) result.Add(current);
                if (distance == range) continue;

                foreach (var edge in grid.Graph.GetNeighbors(current))
                {
                    var n = edge.To;
                    if (visited.ContainsKey(n)) continue;
                    if (!grid.CanPlace(n, fp, ignore: selfGuid)) continue;

                    visited[n] = distance + 1;
                    queue.Enqueue(n);
                }
            }

            return result;
        }

        // ======================================================================
        // Búsqueda con estado (celda, pasos, HP proyectado)
        // ======================================================================

        private sealed class Label
        {
            public GridCoord Coord;
            public int Steps;
            public int HpProj;
            public int Cost;
            public Label Parent;
        }

        private AIPathPlanResult LabelPlan(IGridManager grid, ISpecialTileAIQuery tiles, in AIPathRequest r)
        {
            var profile = r.Personality;
            float minSurvival = profile.MinSurvivalHpPct * r.MaxHp;

            var start = new Label { Coord = r.Origin, Steps = 0, HpProj = r.CurrentHp, Cost = 0 };
            var open = new List<Label> { start };
            var byCoord = new Dictionary<GridCoord, List<Label>> { [r.Origin] = new List<Label> { start } };

            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++)
                    if (open[i].Cost < open[bi].Cost) bi = i;
                var label = open[bi];
                open.RemoveAt(bi);

                if (label.Steps >= r.MaxSteps) continue;

                foreach (var edge in grid.Graph.GetNeighbors(label.Coord))
                {
                    var n = edge.To;
                    if (!grid.IsWalkable(n) || grid.IsOccupied(n)) continue;

                    int hpAtEntry = label.HpProj;
                    int realDamage = 0, penaltyDamage = 0, terrain = 0;

                    var entryDir = CardinalExtensions.FromDelta(label.Coord, n);
                    if (tiles.TryGetTileFor(n, r.SelfGuid, entryDir, out var view))
                    {
                        // Telegraph letal: bloquea la ruta desde acá salvo IA Kamikaze.
                        if (view.TelegraphLethal && !profile.IsKamikaze) continue;

                        realDamage = view.EnterDamage + (view.HasTelegraph ? view.TelegraphDamage : 0);
                        if (view.IsPortal)
                        {
                            // Daño esperado = el de la casilla destino del portal. Un solo
                            // lookup plano — NUNCA se recalcula HazardPenalty recursivo.
                            realDamage = tiles.TryGetTileFor(view.ForcedDestination, r.SelfGuid, entryDir, out var destView)
                                ? destView.EnterDamage + (destView.HasTelegraph ? destView.TelegraphDamage : 0)
                                : 0;
                        }
                        penaltyDamage = realDamage + view.VirtualEnterDamage;

                        if (view.HasForcedMove)
                        {
                            bool destHazardous = tiles.TryGetTileFor(view.ForcedDestination, r.SelfGuid, entryDir, out var fd)
                                && (fd.EnterDamage > 0 || fd.HasTelegraph);
                            bool destCloser = view.ForcedDestination.Manhattan(r.TargetCoord) < n.Manhattan(r.TargetCoord);
                            terrain = ComputeTerrainModifier(view.IsPortal, destHazardous, destCloser);
                        }
                    }

                    // Filtro de supervivencia — '>' estricto: quedar exacto en el umbral descarta.
                    // Solo casillas dañinas: una celda limpia no bloquea a una unidad ya herida.
                    if (realDamage > 0 && !profile.SkipSurvivalFilter
                        && hpAtEntry - realDamage <= minSurvival)
                    {
                        continue;
                    }

                    int hazardPenalty = ComputeHazardPenalty(penaltyDamage, hpAtEntry, profile.Caution);
                    int tileCost = ComputeTileCost(hazardPenalty, terrain);

                    var candidate = new Label
                    {
                        Coord = n,
                        Steps = label.Steps + 1,
                        HpProj = hpAtEntry - realDamage,
                        Cost = label.Cost + tileCost,
                        Parent = label,
                    };

                    if (!TryAddLabel(byCoord, candidate)) continue;
                    open.Add(candidate);
                }
            }

            return SelectDestination(tiles, byCoord, r, profile, minSurvival);
        }

        /// <summary>Frontera de Pareto por celda: se descartan las labels dominadas
        /// (costo ≥, pasos ≥, HP ≤ que otra existente).</summary>
        private static bool TryAddLabel(Dictionary<GridCoord, List<Label>> byCoord, Label candidate)
        {
            if (!byCoord.TryGetValue(candidate.Coord, out var list))
            {
                byCoord[candidate.Coord] = new List<Label> { candidate };
                return true;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var existing = list[i];
                if (existing.Cost <= candidate.Cost
                    && existing.Steps <= candidate.Steps
                    && existing.HpProj >= candidate.HpProj)
                {
                    return false;
                }
            }

            list.RemoveAll(existing => candidate.Cost <= existing.Cost
                && candidate.Steps <= existing.Steps
                && candidate.HpProj >= existing.HpProj);
            list.Add(candidate);
            return true;
        }

        // ======================================================================
        // Selección de destino — DestinationScore + pisar peligro a propósito
        // ======================================================================

        // r y profile van por valor: las lambdas de abajo no pueden capturar parámetros 'in'.
        private AIPathPlanResult SelectDestination(ISpecialTileAIQuery tiles,
            Dictionary<GridCoord, List<Label>> byCoord, AIPathRequest r,
            AIPersonalityProfile profile, float minSurvival)
        {
            // Mejor label por celda (menor costo; empate → mayor HP proyectado).
            var candidates = new List<Label>();
            foreach (var pair in byCoord)
            {
                if (pair.Key == r.Origin) continue;
                Label best = null;
                foreach (var label in pair.Value)
                {
                    if (best == null || label.Cost < best.Cost
                        || (label.Cost == best.Cost && label.HpProj > best.HpProj))
                    {
                        best = label;
                    }
                }
                if (best != null) candidates.Add(best);
            }

            // Hielo/Portal no son destino final: son tránsito (su atracción ya vive en el
            // TerrainModifier). La física igual los resolvería, pero elegirlos como "parada"
            // haría al planner mentirse sobre dónde termina.
            candidates.RemoveAll(label =>
                tiles.TryGetTileFor(label.Coord, r.SelfGuid, DirInto(label), out var v) && v.HasForcedMove);

            if (candidates.Count == 0) return AIPathPlanResult.NoMove;

            int minErr = int.MaxValue;
            foreach (var c in candidates) minErr = Mathf.Min(minErr, Err(c.Coord, r));

            // Info de "opción segura" para TacticalGain: mejor distancia lograble sin peligro.
            int bestSafeDist = r.Origin.Manhattan(r.TargetCoord);
            bool anySafeReachesBand = Err(r.Origin, r) == 0;
            foreach (var c in candidates)
            {
                bool hazardous = tiles.TryGetTileFor(c.Coord, r.SelfGuid, DirInto(c), out var v)
                    && (v.StayDamage > 0 || v.HasTelegraph);
                if (hazardous) continue;
                bestSafeDist = Mathf.Min(bestSafeDist, c.Coord.Manhattan(r.TargetCoord));
                if (Err(c.Coord, r) == 0) anySafeReachesBand = true;
            }

            // Baseline: quedarse quieto (score de la posición actual, costo 0).
            int bestScore = Err(r.Origin, r) * BandWeight;
            Label chosen = null;

            foreach (var c in candidates)
            {
                tiles.TryGetTileFor(c.Coord, r.SelfGuid, DirInto(c), out var view);

                // Regla 5 del GDD: pisar peligro a propósito como destino final.
                if (view.StayDamage > 0 || view.HasTelegraph)
                {
                    if (!profile.SkipSurvivalFilter && c.HpProj - view.StayDamage <= minSurvival) continue;

                    int stayPenaltyDamage = view.StayDamage + view.VirtualEnterDamage
                        + (view.HasTelegraph ? view.TelegraphDamage : 0);
                    int stayPenalty = ComputeHazardPenalty(stayPenaltyDamage, Mathf.Max(1, c.HpProj), profile.Caution);
                    int gainFinal = ComputeTacticalGainFinal(
                        canAttackFromTile: c.Coord.Manhattan(r.TargetCoord) <= r.AttackRange,
                        isOnlyBandReacher: Err(c.Coord, r) == 0 && !anySafeReachesBand,
                        cutsDistance: bestSafeDist - c.Coord.Manhattan(r.TargetCoord) >= 2,
                        selfHealthy: r.CurrentHp >= 0.75f * r.MaxHp,
                        targetLow: r.TargetHpPct >= 0 && r.TargetHpPct <= 50,
                        staysOnDamage: view.StayDamage > 0,
                        staysOnTelegraph: view.HasTelegraph && !view.TelegraphLethal,
                        lowHpAfter: c.HpProj - view.StayDamage < 0.5f * r.MaxHp,
                        _tuning);

                    // Empate ⇒ gana la opción segura ('>' estricto).
                    if (gainFinal <= stayPenalty) continue;
                }

                int benefit = ResolveBenefitValue(tiles, c, view, r, minErr);
                int score = Err(c.Coord, r) * BandWeight + (c.Cost - benefit);
                if (score < bestScore)
                {
                    bestScore = score;
                    chosen = c;
                }
            }

            if (chosen == null) return AIPathPlanResult.NoMove;

            var path = new List<GridCoord>();
            for (var l = chosen; l != null; l = l.Parent) path.Add(l.Coord);
            path.Reverse();
            return new AIPathPlanResult(true, chosen.Coord, path);
        }

        private static Cardinal DirInto(Label label)
            => label.Parent == null
                ? Cardinal.South
                : CardinalExtensions.FromDelta(label.Parent.Coord, label.Coord);

        private static int Err(GridCoord coord, in AIPathRequest r)
        {
            int dist = coord.Manhattan(r.TargetCoord);
            if (r.Intent == MoveIntent.Approach)
                return Mathf.Abs(dist - r.DesiredRange);
            // Kite: 0 cuando alcanzó (o superó) la distancia ideal.
            return Mathf.Max(0, r.DesiredRange - Mathf.Min(dist, r.DesiredRange));
        }

        private int ResolveBenefitValue(ISpecialTileAIQuery tiles, Label c,
            in SpecialTileAIView view, in AIPathRequest r, int minErr)
        {
            switch (view.Benefit)
            {
                case BeneficialTileKind.Healing:
                    // Condición: HP proyectado ≤ 60% máx Y el desvío cuesta ≤ 2 tiles
                    // ("desvío" = exceso de error de banda vs la mejor candidata).
                    if (c.HpProj <= HealMaxHpPct * r.MaxHp && Err(c.Coord, r) - minErr <= HealDetourMaxTiles)
                        return Mathf.CeilToInt((r.MaxHp - c.HpProj) / (float)r.MaxHp * HealBenefitScale);
                    return 0;

                case BeneficialTileKind.Fortress:
                    // Puede atacar este turno desde ahí, o está defendiendo posición (kite).
                    return c.Coord.Manhattan(r.TargetCoord) <= r.AttackRange || r.Intent == MoveIntent.Kite
                        ? FortressBenefit
                        : 0;

                case BeneficialTileKind.Impulse:
                    // Sin tirada real de movimiento la casilla no otorga nada, así que no suma
                    // score.
                    return 0;

                case BeneficialTileKind.SafeZone:
                    return tiles.AnyActiveDangerTelegraph ? SafeZoneBenefit : 0;

                default:
                    return 0;
            }
        }

        // ======================================================================
        // Fórmulas del GDD — puras y testeables con valores exactos
        // ======================================================================

        /// <summary><c>ceil((daño / HP proyectado) × 10 × Caution)</c>; 0 sin fuente de daño.</summary>
        public static int ComputeHazardPenalty(int expectedDamage, int hpProjected, float caution)
        {
            if (expectedDamage <= 0) return 0;
            return Mathf.CeilToInt(expectedDamage / (float)Mathf.Max(1, hpProjected) * 10f * caution);
        }

        /// <summary><c>max(1, 1 + HazardPenalty + TerrainModifier)</c> — nunca baja de 1.</summary>
        public static int ComputeTileCost(int hazardPenalty, int terrainModifier)
            => Mathf.Max(1, 1 + hazardPenalty + terrainModifier);

        /// <summary>Portal: +3 peligro / −1 acerca / 0. Hielo: +2 / −1 / 0. Plano, sin recursión.</summary>
        public static int ComputeTerrainModifier(bool isPortal, bool destinationHazardous, bool destinationCloser)
        {
            if (destinationHazardous) return isPortal ? 3 : 2;
            if (destinationCloser) return -1;
            return 0;
        }

        /// <summary>
        /// <c>TacticalGainFinal = max(0, min(cap8, max(PrimaryGain) + min(cap2, ContextBonus)) − TacticalPenalty)</c>.
        /// PrimaryGain toma el MÁXIMO entre ventajas — nunca la suma (anti-microventajas, GDD).
        /// </summary>
        public static int ComputeTacticalGainFinal(
            bool canAttackFromTile, bool isOnlyBandReacher, bool cutsDistance,
            bool selfHealthy, bool targetLow,
            bool staysOnDamage, bool staysOnTelegraph, bool lowHpAfter,
            AIPathTuningSO tuning)
        {
            int gainAttack = tuning != null ? tuning.GainAttackFromTile : 4;
            int gainBand = tuning != null ? tuning.GainOnlyBandReacher : 3;
            int gainCuts = tuning != null ? tuning.GainCutsDistance : 2;
            int contextCap = tuning != null ? tuning.ContextBonusCap : 2;
            int gainCap = tuning != null ? tuning.TacticalGainCap : 8;
            int penaltyStay = tuning != null ? tuning.PenaltyStayDamage : 2;
            int penaltyTelegraph = tuning != null ? tuning.PenaltyTelegraph : 2;
            int penaltyLowHp = tuning != null ? tuning.PenaltyLowHpAfter : 1;

            int primary = 0;
            if (canAttackFromTile) primary = Mathf.Max(primary, gainAttack);
            if (isOnlyBandReacher) primary = Mathf.Max(primary, gainBand);
            if (cutsDistance) primary = Mathf.Max(primary, gainCuts);

            int context = 0;
            if (selfHealthy) context += 1;
            if (targetLow) context += 1;
            context = Mathf.Min(contextCap, context);

            int gain = Mathf.Min(gainCap, primary + context);

            int penalty = 0;
            if (staysOnDamage) penalty += penaltyStay;
            if (staysOnTelegraph) penalty += penaltyTelegraph;
            if (lowHpAfter) penalty += penaltyLowHp;

            return Mathf.Max(0, gain - penalty);
        }
    }
}
