using System;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Payload typed consumido por <see cref="EffDealDamage"/> — demuestra el patrón
    /// <c>TArgs</c> de <see cref="BaseEffect{TArgs,TValue}"/>. Futuros campos:
    /// <c>CritChance</c>, <c>DamageType</c> elemental, <c>IgnoresArmor</c>, etc.
    /// </summary>
    [Serializable]
    public struct DamageArgs
    {
        /// <summary>Cantidad base antes de mitigación / críticos.</summary>
        public int BaseAmount;
    }
}
