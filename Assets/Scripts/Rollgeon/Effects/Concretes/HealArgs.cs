using System;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Payload typed consumido por <see cref="EffHeal"/>. El combat task downstream
    /// va a extender con <c>Overheal</c>, <c>HealsShield</c>, <c>CritChance</c>, etc.
    /// </summary>
    [Serializable]
    public struct HealArgs
    {
        /// <summary>Cantidad base de curación antes de modificadores.</summary>
        public int BaseAmount;
    }
}
