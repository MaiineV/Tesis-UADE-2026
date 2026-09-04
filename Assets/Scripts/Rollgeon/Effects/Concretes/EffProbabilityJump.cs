using System;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Probability Drive — banda mixta "Salto probabilístico" (Feature#0084,
    /// Items_Activos_Redisenados.md §5, D4 caras 2-3). Teletransporte uniforme entre las
    /// casillas seguras de radio 2-3 alrededor del centro elegido; sin ninguna, degrada a radio
    /// 1 y después a radio 4 antes de rendirse.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffProbabilityJump : BaseEffect
    {
        /// <summary>RNG del sorteo de destino. Público y no serializado: producción usa el
        /// default, los tests inyectan una seed fija.</summary>
        [NonSerialized]
        public System.Random Rng = new System.Random();

        public override string GetEffectName() => "Probability Jump";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[EffProbabilityJump] IGridManager no registrado — no-op.");
                return true;
            }
            if (!TryResolveCenter(context, grid, out var center)) return true;

            ServiceLocator.TryGetService<ISpecialTileService>(out var tiles);
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threats);

            var landing = SafeTileQuery.CollectRing(center, 2, 3, grid, tiles, threats);
            if (landing.Count == 0) landing = SafeTileQuery.CollectRing(center, 1, 1, grid, tiles, threats);
            if (landing.Count == 0) landing = SafeTileQuery.CollectRing(center, 4, 4, grid, tiles, threats);

            if (landing.Count == 0)
            {
                Debug.Log("[EffProbabilityJump] sin destino seguro en ningún radio de fallback — no-op.");
                return true;
            }

            if (!TryGetPathedMovement(out var pathed)) return true;
            pathed.Teleport(context.SourceGuid, landing[Rng.Next(landing.Count)]);
            return true;
        }

        private static bool TryResolveCenter(EffectContext context, IGridManager grid, out GridCoord center)
        {
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord selected)
            {
                center = selected;
                return true;
            }
            return grid.TryGetPosition(context.SourceGuid, out center);
        }

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
