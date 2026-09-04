using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Grid;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Targeting;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Forced;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Grapple Claw (Feature#0085 — Items_Activos_Redisenados.md §3, D6 Gradiente). Ancla un
    /// enemigo movible y lo atrae hacia el jugador, o —si el ancla es sólida/inamovible— avanza
    /// al jugador hacia ella. Con cara 1-2 agrega Cadena Inestable: un enemigo movible cercano a
    /// la trayectoria intermedia se arrastra 1 casilla hacia ella.
    /// </summary>
    /// <remarks>
    /// Un solo grupo de efectos (<see cref="ActiveItemResolution.Gradient"/>): la cara ES la
    /// magnitud de desplazamiento, no hay bandas. El ancla y la dirección salen del
    /// <see cref="ActiveItemRollTriggerContext"/> armado por el flujo de dirección (§A4) — este
    /// efecto no tiene <see cref="Selection"/> propia.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffGrappleClaw : BaseEffect, IDirectionTargetedEffect
    {
        private const int AcquireRange = 6;
        private const int UnstableChainMaxFace = 2;

        // El ancla y la dirección salen del trigger context (§A4), no de un picking propio.
        protected override bool ShowSelection => false;

        /// <summary>RNG del sorteo de Cadena Inestable. Público y no serializado: producción usa
        /// el default, los tests inyectan una seed fija para determinismo.</summary>
        [NonSerialized]
        public System.Random Rng = new System.Random();

        public override string GetEffectName() => "Grapple Claw";

        public IReadOnlyList<GridCoord> PreviewTrajectory(Guid owner, GridCoord origin, Cardinal dir)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
                return Array.Empty<GridCoord>();

            var trace = GridLineTrace.Trace(grid, origin, dir, AcquireRange, owner);

            // Sin nada dentro de rango (ni pared, ni ocupante): no hay ancla — dirección
            // inválida, el servicio la descarta como proxy de selección.
            if (trace.Stop == LineTraceStop.MaxReached) return Array.Empty<GridCoord>();

            var result = new List<GridCoord>(trace.FreeCells) { trace.HitCoord };
            return result;
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (!ActiveItemRollTriggerContext.TryGet(context, out var rc) || rc.Direction == null)
            {
                Debug.LogWarning("[EffGrappleClaw] sin ActiveItemRollTriggerContext/Direction — no-op.");
                return true;
            }

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null
                || !ServiceLocator.TryGetService<IForcedMovementService>(out var forced) || forced == null)
            {
                Debug.LogWarning("[EffGrappleClaw] IGridManager/IForcedMovementService no registrados — no-op.");
                return true;
            }

            var player = context.SourceGuid;
            var dir = rc.Direction.Value;
            var origin = rc.Origin;

            var trace = GridLineTrace.Trace(grid, origin, dir, AcquireRange, player);

            // Direccion sin ancla al momento de resolver (algo cambió entre elegirla y pagar
            // el roll): el roll ya se pagó, no hay nada que hacer — no-op, no false.
            if (trace.Stop == LineTraceStop.MaxReached) return true;

            var liveEnemies = new HashSet<Guid>(CombatantQuery.LiveEnemiesOf(player));
            bool isMovableEnemyAnchor = trace.Stop == LineTraceStop.Occupant
                                        && liveEnemies.Contains(trace.Occupant)
                                        && CombatantQuery.IsMovable(trace.Occupant);

            if (isMovableEnemyAnchor)
            {
                // Ancla movible: se atrae hacia el jugador, frenando adyacente.
                PullResolver.PullToward(forced, grid, trace.Occupant, origin, rc.Face, player);
            }
            else
            {
                // Pared, prop, cofre o enemigo inamovible: el jugador avanza hacia el ancla,
                // cortado antes de la primera casilla dañina y a lo sumo Face casillas.
                var chargeCells = CutBeforeHarmful(trace.FreeCells);
                int tiles = Math.Min(rc.Face, chargeCells.Count);
                if (tiles > 0) forced.Push(player, dir, tiles, player);
            }

            if (rc.Face <= UnstableChainMaxFace)
                ApplyUnstableChain(grid, forced, player, origin, trace, liveEnemies);

            return true;
        }

        // Corta la lista de celdas libres antes de la primera que sea dañina — nunca hace
        // pasar al jugador por una casilla que le cueste vida (mismo criterio que Justa).
        private static List<GridCoord> CutBeforeHarmful(IReadOnlyList<GridCoord> freeCells)
        {
            var result = new List<GridCoord>();
            foreach (var cell in freeCells)
            {
                if (HarmfulTileQuery.IsHarmfulAt(cell)) break;
                result.Add(cell);
            }
            return result;
        }

        /// <summary>
        /// Cadena Inestable (caras 1-2): un enemigo movible a Manhattan 1 de alguna celda
        /// ESTRICTAMENTE intermedia entre la posición original del jugador y el ancla (las
        /// <see cref="LineTraceResult.FreeCells"/> de la traza original, antes de mover a nadie)
        /// se arrastra 1 casilla hacia la celda de cadena más cercana. El ancla nunca es
        /// candidato. Sin candidatos: no-op (solo feedback visual, según el GDD).
        /// </summary>
        private void ApplyUnstableChain(IGridManager grid, IForcedMovementService forced, Guid player,
            GridCoord originalPlayerCoord, in LineTraceResult trace, HashSet<Guid> liveEnemies)
        {
            var chainCells = new List<GridCoord>(trace.FreeCells);
            if (chainCells.Count == 0) return;

            var chainSet = new HashSet<GridCoord>(chainCells);
            var candidates = new List<(Guid enemy, GridCoord nearestChainCell)>();

            foreach (var enemy in liveEnemies)
            {
                if (enemy == trace.Occupant) continue; // el ancla nunca es candidato
                if (!CombatantQuery.IsMovable(enemy)) continue;
                if (!grid.TryGetPosition(enemy, out var enemyCoord)) continue;
                if (chainSet.Contains(enemyCoord)) continue;

                GridCoord nearest = default;
                int nearestDist = int.MaxValue;
                foreach (var cell in chainCells)
                {
                    int d = enemyCoord.Manhattan(cell);
                    if (d < nearestDist) { nearestDist = d; nearest = cell; }
                }
                if (nearestDist == 1) candidates.Add((enemy, nearest));
            }

            if (candidates.Count == 0) return;

            var picked = candidates[Rng.Next(candidates.Count)];
            if (!grid.TryGetPosition(picked.enemy, out var pickedCoord)) return;
            var chainDir = CardinalExtensions.FromDelta(pickedCoord, picked.nearestChainCell);
            forced.Push(picked.enemy, chainDir, 1, player);
        }
    }
}
