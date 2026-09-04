using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Effects
{
    /// <summary>
    /// Torbellino: todos los enemigos del dueño se teletransportan a celdas libres al azar de
    /// la sala. Las celdas candidatas son las alcanzables caminando desde el enemigo (misma
    /// componente conexa, sin islas) — reusa <see cref="IMovementService.GetReachableTiles"/>.
    /// Stacking GDD: redundante — solo la primera copia viva actúa.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffTeleportEnemiesRandomly : BaseEffect, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [MinValue(1)]
        [Tooltip("Radio del BFS de celdas candidatas (grande = toda la sala).")]
        public int SearchRange = 64;

        /// <summary>RNG inyectable para tests determinísticos.</summary>
        [NonSerialized] public System.Random Rng;

        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Teleport Enemies Randomly";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null || context.SourceGuid == Guid.Empty) return false;
            if (context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) && trig.Slot != null)
            {
                MovementLaneCopies.Count(trig.Slot.Value, out bool first);
                if (!first) return true;
            }

            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!ServiceLocator.TryGetService<IMovementService>(out var movement) || movement == null) return false;
            var pathed = movement as IPathedMovementService;
            if (pathed == null && !ServiceLocator.TryGetService<IPathedMovementService>(out pathed)) return false;
            if (pathed == null) return false;

            var rng = Rng ?? new System.Random();
            var enemies = new List<Guid>();
            foreach (var e in query.GetAllEnemiesOf(context.SourceGuid))
            {
                if (e != null) enemies.Add(e.Guid);
            }

            foreach (var enemy in enemies)
            {
                if (!grid.TryGetPosition(enemy, out var from)) continue;
                var candidates = movement.GetReachableTiles(from, SearchRange);
                if (candidates.Count == 0) continue;
                var to = candidates[rng.Next(candidates.Count)];
                pathed.Teleport(enemy, to);
            }
            return true;
        }
    }
}
