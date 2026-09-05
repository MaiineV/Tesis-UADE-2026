using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Status;
using Rollgeon.Grid;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Targeting;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Bottle'o Thunder (D4 Jerarquía — Items_Activos_Redisenados.md §7). Aturde al objetivo
    /// primario ya seleccionado y encadena rebotes hasta <c>Magnitude</c> (= la cara del D4):
    /// cada salto elige el enemigo aturdible más cercano al último golpeado, con línea de visión
    /// limpia, que todavía no fue impactado en esta activación.
    /// </summary>
    /// <remarks>
    /// Implementa <see cref="IActiveItemTargetFilter"/>: el servicio lo usa para restringir qué
    /// celdas de la selección normal (§A4 generalizado) son un objetivo primario válido —
    /// enemigo vivo, aturdible y con línea de visión desde el jugador.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffChainStun : BaseEffect, IActiveItemTargetFilter
    {
        [Title("Cadena de Aturdimiento")]
        [MinValue(1)]
        [Tooltip("Turnos de Aturdido que recibe cada objetivo golpeado.")]
        public int Turns = 1;

        [MinValue(1)]
        [Tooltip("Rango Manhattan máximo de cada rebote desde el último objetivo golpeado.")]
        public int BounceRange = 2;

        /// <summary>RNG del desempate entre rebotes equidistantes. Público y no serializado:
        /// producción usa el default, los tests inyectan una seed fija.</summary>
        [NonSerialized]
        private System.Random _rng;

        /// <remarks>
        /// Propiedad perezosa y no un campo inicializado: Odin instancia los efectos del
        /// asset sin correr constructores ni inicializadores de campo, asi que un
        /// <c>= new System.Random()</c> queda en null en runtime (Probability Drive
        /// tiraba NullReference al resolver — ronda de testers 2026-09-04).
        /// </remarks>
        public System.Random Rng
        {
            get { return _rng ?? (_rng = new System.Random()); }
            set { _rng = value; }
        }

        public override string GetEffectName() => "Chain Stun";

        public bool IsValidTarget(Guid owner, GridCoord coord)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!grid.TryGetOccupant(coord, out var occupant) || occupant == Guid.Empty) return false;
            if (!CombatantQuery.IsStunnable(occupant)) return false;
            if (!CombatantQuery.LiveEnemiesOf(owner).Contains(occupant)) return false;
            if (!grid.TryGetPosition(owner, out var ownerCoord)) return false;
            return GridLineOfSight.HasClearLine(grid, ownerCoord, coord, owner, occupant);
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (!ActiveItemRollTriggerContext.TryGet(context, out var rc))
            {
                Debug.LogWarning("[EffChainStun] sin ActiveItemRollTriggerContext — no-op.");
                return true;
            }
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[EffChainStun] IGridManager no registrado — no-op.");
                return true;
            }

            var primaryCoord = context.SelectionResult?.FirstSelectedCoord;
            if (primaryCoord == null
                || !grid.TryGetOccupant(primaryCoord.Value, out var primary)
                || primary == Guid.Empty)
            {
                Debug.Log("[EffChainStun] el objetivo primario ya no está en la celda seleccionada " +
                          "— no-op (el roll ya se pagó).");
                return true;
            }

            var owner = context.SourceGuid;
            var liveEnemies = new HashSet<Guid>(CombatantQuery.LiveEnemiesOf(owner));
            if (!liveEnemies.Contains(primary))
            {
                Debug.Log("[EffChainStun] el objetivo primario ya no es un enemigo vivo " +
                          "— no-op (el roll ya se pagó).");
                return true;
            }

            var hit = new List<Guid> { primary };
            var lastCoord = primaryCoord.Value;

            while (hit.Count < rc.Magnitude)
            {
                var ties = new List<Guid>();
                int bestDist = int.MaxValue;

                foreach (var enemy in liveEnemies)
                {
                    if (hit.Contains(enemy)) continue;
                    if (!CombatantQuery.IsStunnable(enemy)) continue;
                    if (!grid.TryGetPosition(enemy, out var enemyCoord)) continue;

                    int dist = lastCoord.Manhattan(enemyCoord);
                    if (dist > BounceRange) continue;
                    if (!GridLineOfSight.HasClearLine(grid, lastCoord, enemyCoord, owner, enemy)) continue;

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        ties.Clear();
                        ties.Add(enemy);
                    }
                    else if (dist == bestDist)
                    {
                        ties.Add(enemy);
                    }
                }

                if (ties.Count == 0) break; // sin candidato dentro de rango/LoS — la cadena termina acá

                var next = ties[Rng.Next(ties.Count)];
                hit.Add(next);
                grid.TryGetPosition(next, out lastCoord);
            }

            if (!ServiceLocator.TryGetService<IStunService>(out var stun) || stun == null)
            {
                Debug.LogWarning("[EffChainStun] IStunService no registrado — nadie pierde el turno.");
                return true;
            }

            foreach (var target in hit) stun.ApplyStun(target, Turns);

            return true;
        }
    }
}
