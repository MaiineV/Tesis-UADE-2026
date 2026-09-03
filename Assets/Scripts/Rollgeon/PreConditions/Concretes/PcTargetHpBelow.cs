using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// "El objetivo está herido": HP actual del <c>OpponentGuid</c> ≤ <see cref="Percent"/>
    /// de su vida máxima. Espejo de <see cref="PcOwnerHpBelow"/> para el target — umbral
    /// de remate (Ejecutor: +12 si el enemigo está al 25% o menos).
    /// </summary>
    /// <remarks>
    /// Solo tiene sentido en hooks con objetivo real (ComboPlayed, daño). En ComboMatched
    /// el canal dados rellena TargetGuid con el propio jugador — ahí devuelve false.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcTargetHpBelow : BasePreCondition
    {
        [Range(0f, 1f)]
        [Tooltip("Ratio de HP. 0.25 = dispara cuando el HP del objetivo es ≤ 25% del máximo.")]
        public float Percent = 0.25f;

        public override string ConditionName => $"Target HP ≤ {Percent:P0}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null) return false;

            Guid target = context.OpponentGuid;
            if (target == Guid.Empty || target == context.OwnerGuid) return false;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return false;
            var hp = attrs.GetAttribute<Health>(target);
            if (hp == null) return false;

            if (!MaxHpResolver.TryResolve(target, out int max)) return false;
            return (float)hp.ModifiedValue / max <= Percent;
        }
    }
}
