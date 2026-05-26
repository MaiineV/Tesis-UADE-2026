using System;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Polymorphic base for runtime stored values in a <see cref="BaseBehavior"/>.
    /// TECHNICAL.md 9.2.
    /// </summary>
    [Serializable]
    public abstract class BaseBehaviorStoredValue
    {
    }

    [Serializable]
    public class FloatBehaviorValue : BaseBehaviorStoredValue
    {
        public float Value;
    }

    /// <summary>
    /// Payload for floating damage/heal numbers written by EffDealDamage/EffHeal during apply.
    /// </summary>
    [Serializable]
    public class FloatingNumberBehaviorValue : BaseBehaviorStoredValue
    {
        public float Value;
        public Vector3 Offset;
        public Guid TargetEntityGuid;
        public float Delay;
    }

    /// <summary>
    /// Payload for knockback / impulse vectors written by <c>EffApplyImpulse</c>.
    /// Consumed by downstream feedback (camera shake magnitude, hitstop scaling) and,
    /// eventually, by a physics/animation layer that moves the target pawn.
    /// </summary>
    [Serializable]
    public class ImpulseBehaviorValue : BaseBehaviorStoredValue
    {
        public Vector3 Impulse;
        public Guid TargetEntityGuid;
    }
}
