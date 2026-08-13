using System;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Anula el daño del combo en curso (<c>BlockComboDamage</c> del scratch del dispatch).
    /// Idempotente — múltiples triggers bloqueando no cambian el resultado. Componer con
    /// PreConditions para el gating ("sin oro no hay daño", chance de fallo, etc.).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffBlockComboDamage : BaseEffect,
        IComboScratchWriter, IRequiresTriggerContext<ScratchTriggerContext>
    {
        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Block Combo Damage";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null
                || !context.TryGetTriggerContext<ScratchTriggerContext>(out var trig)
                || trig.Scratch == null)
            {
                Debug.LogWarning("[EffBlockComboDamage] sin ScratchTriggerContext — este efecto " +
                                 "solo funciona dentro de un dispatch de trigger de combo.");
                return false;
            }

            trig.Scratch.BlockComboDamage = true;
            return true;
        }
    }
}
