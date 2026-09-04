using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Probability Drive — banda negativa "Distorsión" (Feature#0084, Items_Activos_Redisenados.md
    /// §5, D4 cara 1). Teletransporte seguro cerca de la casilla central elegida y, después, swap
    /// de posiciones entre dos enemigos movibles del área — sin daño, solo reordena la formación.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffProbabilityDistortion : BaseEffect
    {
        [Title("Probability Drive — Distorsión")]
        [MinValue(1)]
        [Tooltip("Radio Manhattan desde el centro dentro del que se sortean los dos enemigos a intercambiar.")]
        public int SwapRadius = 4;

        /// <summary>RNG del teleport/swap. Público y no serializado: producción usa el default,
        /// los tests inyectan una seed fija.</summary>
        [NonSerialized]
        public System.Random Rng = new System.Random();

        public override string GetEffectName() => "Probability Distortion";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[EffProbabilityDistortion] IGridManager no registrado — no-op.");
                return true;
            }
            if (!TryResolveCenter(context, grid, out var center)) return true;

            var player = context.SourceGuid;
            ServiceLocator.TryGetService<ISpecialTileService>(out var tiles);
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threats);

            var landing = SafeTileQuery.CollectRing(center, 0, 1, grid, tiles, threats);
            if (landing.Count == 0) landing = SafeTileQuery.CollectRing(center, 0, 4, grid, tiles, threats);

            if (landing.Count == 0)
            {
                Debug.Log("[EffProbabilityDistortion] sin destino seguro en radio 1/4 — teleport omitido.");
            }
            else if (TryGetPathedMovement(out var pathed))
            {
                pathed.Teleport(player, landing[Rng.Next(landing.Count)]);
            }

            var eligible = new List<Guid>();
            foreach (var enemy in CombatantQuery.LiveEnemiesOf(player))
            {
                if (!CombatantQuery.IsMovable(enemy)) continue;
                if (!grid.TryGetPosition(enemy, out var coord)) continue;
                if (center.Manhattan(coord) > SwapRadius) continue;
                eligible.Add(enemy);
            }

            if (eligible.Count >= 2 && TryGetPathedMovement(out var pathedSwap))
            {
                int i = Rng.Next(eligible.Count);
                int j;
                do { j = Rng.Next(eligible.Count); } while (j == i);
                pathedSwap.Swap(eligible[i], eligible[j]);
            }

            return true;
        }

        // Centro = primer coord seleccionado por el jugador antes de tirar; sin selección
        // (tests/flujos degradados), cae a la posición del propio jugador.
        private static bool TryResolveCenter(EffectContext context, IGridManager grid, out GridCoord center)
        {
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord selected)
            {
                center = selected;
                return true;
            }
            return grid.TryGetPosition(context.SourceGuid, out center);
        }

        // Mismo patrón que EffTeleportEnemiesRandomly: IMovementService puede implementar
        // IPathedMovementService directo, o el servicio vivir registrado aparte.
        private static bool TryGetPathedMovement(out IPathedMovementService pathed)
        {
            pathed = null;
            if (ServiceLocator.TryGetService<IMovementService>(out var movement) && movement != null)
                pathed = movement as IPathedMovementService;
            if (pathed == null) ServiceLocator.TryGetService<IPathedMovementService>(out pathed);
            return pathed != null;
        }
    }
}
