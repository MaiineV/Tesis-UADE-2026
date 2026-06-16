using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si <c>Health</c> del owner es estrictamente menor al
    /// <see cref="Percent"/> (0..1) de su HP máximo. Reemplaza al viejo
    /// <c>AICond_HPBelow</c> bajo el catálogo central de PC. TECHNICAL.md §8.2.
    /// </summary>
    /// <remarks>
    /// El MaxHp llega via <see cref="PreConditionContext.OwnerMaxHp"/> (lo popula
    /// el bridge <c>AIContextPcExtensions.BuildPcContext</c>). Si no está, fallback
    /// a <c>Health.ModifiedValue</c> sin baseline → la PC devuelve false (no podemos
    /// comparar contra ratio). Si el owner no tiene Health registrada, false.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcOwnerHpBelow : BasePreCondition
    {
        [Range(0f, 1f)]
        [Tooltip("Ratio de HP. 0.5 = dispara cuando HP < 50% del máximo.")]
        public float Percent = 0.5f;

        public override string ConditionName => $"Owner HP < {Percent:P0}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs)) return false;

            var hp = attrs.GetAttribute<Health>(context.OwnerGuid);
            if (hp == null) return false;

            int max = context.OwnerMaxHp ?? 0;
            if (max <= 0) return false;

            float ratio = (float)hp.ModifiedValue / max;
            return ratio < Percent;
        }
    }
}
