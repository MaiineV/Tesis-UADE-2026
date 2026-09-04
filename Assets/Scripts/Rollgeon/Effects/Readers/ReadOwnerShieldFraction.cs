using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// <c>E = max(Min, ceil|floor(Fraction × escudo actual del owner))</c> (Feature#0085,
    /// Coin Shield banda impar: "E = max(1, ceil(50% del escudo actual del jugador))").
    /// Sin <see cref="AttributesManager"/> registrado: devuelve <see cref="Min"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadOwnerShieldFraction : EffectIntReader
    {
        [Range(0f, 1f)]
        public float Fraction = 0.5f;

        [Tooltip("true = redondea para arriba (ceil). false = para abajo (floor).")]
        public bool Ceil = true;

        [MinValue(0)]
        public int Min = 1;

        public override int Read(EffectContext context)
        {
            if (context == null) return Min;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return Min;

            var ownerGuid = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;
            int shield = attrs.GetAttributeModifiedValue<Shield, int>(ownerGuid);
            float raw = shield * Fraction;
            int computed = Ceil ? Mathf.CeilToInt(raw) : Mathf.FloorToInt(raw);
            return Math.Max(Min, computed);
        }
    }
}
