using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// Reader que devuelve <c>floor(sqrt(oro_actual × Factor))</c> — bono de daño con
    /// retornos decrecientes en vez de 1:1 (BUG-080: "El Egoísta" sumaba TODO el oro
    /// actual, directo y permanente, al Attack BASE en cada golpe). Con
    /// <see cref="Factor"/> = 5 (default de diseño): oro 0/1/5/20/45 → bono 0/2/5/10/15.
    /// </summary>
    /// <remarks>
    /// Se computa en el momento del golpe (via <c>EffectContext</c>, sin side effects) —
    /// a diferencia del viejo <c>EffModifyIntAttribute</c> que mutaba el atributo, este
    /// reader es de solo lectura y no deja rastro entre golpes.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCurrentGoldSqrtScaled : EffectIntReader
    {
        [MinValue(0.01f)]
        [Tooltip("Multiplicador dentro de la raíz: bono = floor(sqrt(oro_actual × Factor)). " +
                 "Default 5 (diseño BUG-080): oro 0/1/5/20/45 → bono 0/2/5/10/15.")]
        public float Factor = 5f;

        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return 0;

            int gold = Mathf.Max(0, economy.CurrentGold);
            return Mathf.FloorToInt(Mathf.Sqrt(gold * Factor));
        }
    }
}
