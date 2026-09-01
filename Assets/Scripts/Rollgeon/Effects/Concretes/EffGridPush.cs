using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Tiles.Forced;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Empuje de grilla genérico para Behaviors enemigos (Charger del GDD: "daño + empuje
    /// 1 casilla; si está bloqueado, +50% en su lugar"). Empuja al target en la dirección
    /// source → target vía <see cref="IForcedMovementService"/> y, si no avanzó ni una
    /// casilla, cobra un bono = ATK del source × <see cref="_blockedBonusMultiplier"/>.
    /// </summary>
    /// <remarks>
    /// A diferencia de <see cref="EffClassSkillPush"/> (Habilidad del Guerrero): sin tabla de
    /// combos, sin cadena de choques y <b>sin stun</b> — el GDD del Charger no lo pide, y el
    /// resolver de la habilidad stunea siempre contra pared. Un target multi-celda usa el
    /// camino físico puro de Fase B (rect entero o bloqueado).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffGridPush : BaseEffect
    {
        [Title("Empuje")]
        [SerializeField, MinValue(1)]
        [Tooltip("Casillas que empuja, en la dirección source → target (cardinal).")]
        private int _distance = 1;

        [SerializeField, MinValue(0f)]
        [Tooltip("Si el empuje quedó bloqueado (no avanzó ni una casilla), daño extra = " +
                 "ATK del source × este multiplicador, por el pipeline. 0 = sin bono.")]
        private float _blockedBonusMultiplier = 0.5f;

        [SerializeField]
        [Tooltip("AttackKind del daño de bono por bloqueo.")]
        private AttackKind _attackKind = AttackKind.BasicAttack;

        public override string GetEffectName() => "Grid Push";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var targets = ResolveTargetGuids(context);
            if (targets.Count == 0) return true;

            if (!ServiceLocator.TryGetService<IForcedMovementService>(out var forced) || forced == null)
            {
                Debug.LogWarning("[EffGridPush] IForcedMovementService no registrado — sin empuje. " +
                                 "Agregá ForcedMovementServiceBootstrap a ExtraServices.");
                return true;
            }
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return true;

            var source = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;
            if (!grid.TryGetPosition(source, out var sourceCoord)) return true;

            foreach (var target in targets)
            {
                if (target == source) continue;

                // Dirección contra la celda del rect del target más cercana al source — con un
                // target multi-celda el ancla puede quedar en diagonal (mismo razonamiento que
                // ClassSkillPushResolver); para 1×1 es la única celda.
                var nearestCell = sourceCoord;
                int nearestDist = int.MaxValue;
                foreach (var cell in grid.OccupiedCells(target))
                {
                    int d = sourceCoord.Manhattan(cell);
                    if (d < nearestDist) { nearestDist = d; nearestCell = cell; }
                }
                if (nearestDist == int.MaxValue) continue; // target ya no está en la grilla

                var dir = CardinalExtensions.FromDelta(sourceCoord, nearestCell);
                var move = forced.Push(target, dir, _distance, source);

                // "Si está bloqueado, +50% en su lugar" (GDD Charger): bloqueado = no avanzó
                // ni una casilla. Un empuje parcial ya movió, así que no cobra bono.
                if (move.TilesTraveled == 0 && _blockedBonusMultiplier > 0f)
                    DealBlockedBonus(source, target);
            }

            return true;
        }

        private void DealBlockedBonus(Guid source, Guid target)
        {
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null)
                return;

            int attack = 0;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
            {
                var stat = attrs.GetAttribute<Attack>(source);
                if (stat != null) attack = stat.Value;
            }

            int bonus = Mathf.RoundToInt(attack * _blockedBonusMultiplier);
            if (bonus <= 0) return;

            pipeline.Resolve(new DamageContext
            {
                SourceId = source,
                TargetId = target,
                BaseDamage = bonus,
                Kind = _attackKind,
            });
        }

        // Mismo criterio que EffDealDamage: celdas seleccionadas → ocupantes dedup; sin
        // selección, TargetGuid.
        private static List<Guid> ResolveTargetGuids(EffectContext context)
        {
            var result = new List<Guid>();

            if (context.SelectionResult?.SelectedTargets != null
                && ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                result = grid.DistinctOccupants(
                    System.Linq.Enumerable.Select(context.SelectionResult.SelectedTargets, t => t.Coord));
            }

            if (result.Count == 0 && context.TargetGuid != Guid.Empty)
                result.Add(context.TargetGuid);

            return result;
        }
    }
}
