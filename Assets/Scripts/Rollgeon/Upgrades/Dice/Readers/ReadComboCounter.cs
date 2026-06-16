using System;
using Patterns;
using Rollgeon.Combos.Counters;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// Reader que devuelve el contador Balatro-style de un combo específico via
    /// <see cref="IComboCountersService"/>. Permite triggers que escalan con la
    /// cantidad de veces que el jugador matcheó ese combo en la run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ej. designer setea <c>ComboId = "combo.par"</c> y compone con un trigger
    /// "+1 daño por cada count" → un dado escalando con la run.
    /// </para>
    /// <para>
    /// Fuera de run el service degrada a 0 — el reader devuelve 0 sin warning.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadComboCounter : EffectIntReader
    {
        [Tooltip("ID canónico del combo (ej. 'combo.par', 'combo.trio'). " +
                 "Debe matchear el ComboId de un BaseComboSO del catalogo.")]
        public string ComboId;

        public override int Read(EffectContext context)
        {
            if (string.IsNullOrEmpty(ComboId)) return 0;
            if (!ServiceLocator.TryGetService<IComboCountersService>(out var counters) || counters == null)
                return 0;
            return counters.GetCount(ComboId);
        }
    }
}
