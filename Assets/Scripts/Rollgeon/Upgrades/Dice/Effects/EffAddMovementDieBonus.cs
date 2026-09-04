using System;
using Rollgeon.Effects;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Effects
{
    /// <summary>
    /// Torbellino: suma <see cref="Amount"/> a la TIRADA del dado de Movimiento en el hook
    /// <c>MovementDieRolled</c>. Escribe al scratch (<see cref="EnchantmentScratch.MovementDieBonus"/>)
    /// y no a <c>MoveRange</c>: el bono vale para esa tirada, el dado lo muestra como chip con el
    /// icono del encantamiento (patrón Botas) y no se arrastra a un segundo Mover del turno.
    /// </summary>
    /// <remarks>
    /// Stacking GDD: redundante por default — solo la primera copia viva suma
    /// (<see cref="OnlyFirstCopy"/>). Fuera del hook (sin scratch) no hace nada y corta la cadena.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffAddMovementDieBonus : BaseEffect, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Tooltip("Cuánto suma a la cara tirada (negativo resta; el rango efectivo nunca baja de 1).")]
        public int Amount = 2;

        [Tooltip("Solo la primera copia viva del encantamiento suma (stacking redundante).")]
        public bool OnlyFirstCopy = true;

        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Add Movement Die Bonus";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (!context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Scratch == null)
                return false;

            if (OnlyFirstCopy && trig.Slot != null)
            {
                MovementLaneCopies.Count(trig.Slot.Value, out bool first);
                if (!first) return true;
            }

            trig.Scratch.MovementDieBonus += Amount;
            return true;
        }
    }
}
