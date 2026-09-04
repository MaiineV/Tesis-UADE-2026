using System;
using Rollgeon.Combat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si existe al menos un enemigo vivo de <c>OwnerGuid</c> (Feature#0084, Blood
    /// Transfusion): con <see cref="ExcludeBloodless"/> exige que al menos uno NO tenga el
    /// tag Bloodless.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcEligibleEnemyExists : BasePreCondition
    {
        [Tooltip("Si true, exige que al menos un enemigo elegible NO sea Bloodless.")]
        public bool ExcludeBloodless = true;

        public override string ConditionName => ExcludeBloodless
            ? "Eligible enemy exists (excludes Bloodless)"
            : "Eligible enemy exists";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;

            var enemies = CombatantQuery.LiveEnemiesOf(context.OwnerGuid);
            if (enemies.Count == 0) return false;
            if (!ExcludeBloodless) return true;

            foreach (var enemy in enemies)
                if (CombatantQuery.IsEligibleForBlood(enemy)) return true;
            return false;
        }
    }
}
