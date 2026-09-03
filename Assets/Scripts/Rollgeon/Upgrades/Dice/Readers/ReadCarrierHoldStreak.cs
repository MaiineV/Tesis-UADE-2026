using System;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// "Ancla": tiradas consecutivas que el dado CARRIER lleva guardado
    /// (<see cref="IDiceHoldStreakService"/>) × <see cref="PerRoll"/>, con tope en
    /// <see cref="MaxRolls"/> tiradas. Se autora en ComboMatched con
    /// <c>RequireCarrierParticipates</c> para que el bono entre a N del preview y del golpe.
    /// </summary>
    /// <remarks>Devuelve 0 sin trigger context con Slot o sin el service registrado.</remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCarrierHoldStreak : EffectIntReader
    {
        [Tooltip("Bono por cada tirada que el dado pasó guardado.")]
        public int PerRoll = 5;

        [Tooltip("Tope de tiradas guardadas que cuentan. 0 = sin tope.")]
        [MinValue(0)]
        public int MaxRolls = 3;

        public override int Read(EffectContext context)
        {
            if (context == null) return 0;
            if (!context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return 0;
            if (!ServiceLocator.TryGetService<IDiceHoldStreakService>(out var streaks) || streaks == null)
                return 0;

            int rolls = streaks.GetStreak(trig.Slot.Value.BagSlotIndex);
            if (MaxRolls > 0 && rolls > MaxRolls) rolls = MaxRolls;
            return rolls * PerRoll;
        }
    }
}
