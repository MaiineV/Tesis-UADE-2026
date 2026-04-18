using System;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Payload typed consumido por <see cref="EffDamage"/> — demuestra el patrón
    /// <c>TArgs</c> de <see cref="BaseEffect{TArgs,TValue}"/>. El combat task downstream
    /// (T100b / T103) va a extender estos campos con <c>CritChance</c>, <c>DamageType</c>
    /// elemental, <c>IgnoresArmor</c>, etc.
    /// </summary>
    [Serializable]
    public struct DamageArgs
    {
        /// <summary>Cantidad base antes de mitigación / críticos.</summary>
        public int BaseAmount;
    }
}
