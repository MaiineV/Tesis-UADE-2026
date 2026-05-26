using System;
using Patterns;
using Rollgeon.Combat.FirstRoll;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Pasa solo si la entidad <c>OwnerGuid</c> aun no resolvio ningun roll desde el ultimo
    /// <c>OnCombatStart</c>. Consume el <see cref="IFirstRollTracker"/>.
    /// TECHNICAL.md §8.2 + §6 (Berserker "Primer golpe ×3").
    /// <para>
    /// Si no hay <see cref="IFirstRollTracker"/> registrado o el <c>OwnerGuid</c> es vacio,
    /// evalua <c>false</c>.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCFirstRollOfCombat : BasePreCondition
    {
        public override string ConditionName => "FirstRollOfCombat(owner)";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null) return false;
            if (context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IFirstRollTracker>(out var tracker) || tracker == null) return false;
            return tracker.IsFirstRoll(context.OwnerGuid);
        }
    }
}
