using System;
using Rollgeon.Effects.Readers;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Triggers.Concretes
{
    /// <summary>
    /// Versión del trigger genérico para el canal de combo passives. Aplica una
    /// operación (Add/Subtract/Multiply/Set) sobre un recurso (oro o stat) cuando
    /// matchea el combo target de la pasiva. Escribe al mismo <see cref="EnchantmentScratch"/>
    /// compartido entre canales.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ModifyResourceComboPassiveTrigger : IOnComboPassiveMatchedTrigger
    {
        [Title("Filtro de combo")]
        [InfoBox("AnyCombo dispara con el combo target de la pasiva. ComboIds restringe a IDs concretos.")]
        public ComboFilter Filter = new ComboFilter();

        [Title("Qué modifica")]
        public ResourceTarget Target = ResourceTarget.Gold;

        public ResourceOperation Operation = ResourceOperation.Add;

        [Title("Cantidad")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Amount;

        public void OnComboMatched(ComboPassiveContext ctx)
        {
            if (ctx?.Scratch == null) return;
            if (!Filter.Matches(ctx.ComboId)) return;
            int amount = Amount != null ? Amount.Read(ctx.Effect) : 0;
            ctx.Scratch.Modify(Target, Operation, amount);
        }
    }
}
