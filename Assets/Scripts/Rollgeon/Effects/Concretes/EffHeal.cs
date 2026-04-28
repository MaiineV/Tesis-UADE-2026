using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Resuelve y aplica curación al target vía <see cref="IHealPipeline"/>. Escribe el
    /// valor curado bajo <see cref="BehaviorValueKey.FloatingHeal"/> para consumo del
    /// feedback downstream. TECHNICAL.md §8.7, §9.5, §17.M.
    /// </summary>
    /// <remarks>
    /// Atómico: resuelve el target desde <see cref="EffectContext.SelectionResult"/>
    /// o <see cref="EffectContext.SourceGuid"/> como fallback (heal self). Si
    /// <see cref="IHealPipeline"/> no está registrado, aborta la cadena (§8.8).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffHeal : BaseEffect<HealArgs, int>,
        IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior
    {
        [Title("Heal")]
        [SerializeField, MinValue(0), MaxValue(999)]
        [Tooltip("Curación base antes de pipeline (overheal, shields).")]
        private int _baseAmount = 10;

        [SerializeField]
        [Tooltip("Si true, BaseAmount es porcentaje del max HP del target.")]
        private bool _isPercentOfMax;

        [SerializeField]
        [Tooltip("Si true, ignora BaseAmount y rolea DiceCount × dDiceFaces. Ej: 1d10 = aleatorio entre 1 y 10.")]
        private bool _useDiceRoll;

        [SerializeField, MinValue(1)]
        [ShowIf(nameof(_useDiceRoll))]
        [Tooltip("Cantidad de dados a rolear cuando UseDiceRoll está activo.")]
        private int _diceCount = 1;

        [SerializeField, MinValue(2)]
        [ShowIf(nameof(_useDiceRoll))]
        [Tooltip("Caras del dado (ej. 10 = d10, rango [1, 10] inclusive).")]
        private int _diceFaces = 10;

        [SerializeField]
        [Tooltip("Tag libre para logging/telemetría — ej. 'potion', 'support.heal'.")]
        private string _sourceTag = "eff.heal";

        public override string GetEffectName() => "Heal";

        protected override HealArgs ResolveArgs(EffectContext context) =>
            new HealArgs { BaseAmount = ResolveBaseAmount() };

        protected override int ResolveValue(EffectContext context) => ResolveBaseAmount();

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var amount = ResolveArgs(context).BaseAmount;
            if (amount <= 0) return true;

            var targetGuid = Guid.Empty;
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord coord)
            {
                if (ServiceLocator.TryGetService<IGridManager>(out var grid))
                    grid.TryGetOccupant(coord, out targetGuid);
            }

            if (targetGuid == Guid.Empty)
            {
                Debug.LogWarning("[EffHeal] No target resuelto — aborta cadena.");
                return false;
            }
            
            int resolvedHeal = amount;
            if (ServiceLocator.TryGetService<IHealPipeline>(out var pipeline) && pipeline != null)
            {
                var healCtx = new HealContext
                {
                    SourceId = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid,
                    TargetId = targetGuid,
                    BaseHeal = amount,
                    IsPercentOfMax = _isPercentOfMax,
                    SourceTag = _sourceTag,
                };
                pipeline.Resolve(healCtx);
                resolvedHeal = healCtx.FinalHeal;
            }
            else
            {
                Debug.LogWarning("[EffHeal] IHealPipeline no registrado — usando amount crudo.");
            }

            if (context.SourceBehavior != null)
            {
                context.SourceBehavior.SetBehaviorValue(
                    BehaviorValueKey.FloatingHeal,
                    new FloatingNumberBehaviorValue
                    {
                        Value = resolvedHeal,
                        TargetEntityGuid = targetGuid,
                    });
            }

            return true;
        }

        private static Guid ResolveTargetGuid(EffectContext context)
        {
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord coord
                && ServiceLocator.TryGetService<IGridManager>(out var grid)
                && grid.TryGetOccupant(coord, out var occupant)
                && occupant != Guid.Empty)
                return occupant;
            return context.SourceGuid;
        }

        private int ResolveBaseAmount()
        {
            if (!_useDiceRoll) return _baseAmount;

            int faces = Mathf.Max(2, _diceFaces);
            int count = Mathf.Max(1, _diceCount);
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += UnityEngine.Random.Range(1, faces + 1);
            }
            return sum;
        }
    }
}
