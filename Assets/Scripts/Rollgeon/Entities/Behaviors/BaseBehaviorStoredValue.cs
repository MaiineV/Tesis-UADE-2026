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
    /// Payload for floating damage/heal numbers written by EffDamage/EffHeal during apply.
    /// </summary>
    [Serializable]
    public class FloatingNumberBehaviorValue : BaseBehaviorStoredValue
    {
        public float Value;
        public Vector3 Offset;
        public Guid TargetEntityGuid;
        public float Delay;
    }
}
