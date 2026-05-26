using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// "Evil": cuando matchea un combo, intenta gastar <see cref="Cost"/> oro. Si
    /// alcanza, suma <see cref="Bonus"/> daño al combo. Si no, no aplica el bonus
    /// (no falla el combo). Hook: <c>OnComboMatched</c>. Cubre el ejemplo del
    /// usuario: "Quitarme oro, pero haga más daño".
    /// </summary>
    /// <remarks>
    /// El costo se aplica via <c>scratch.BonusGold -= cost</c> — el service de
    /// Phase 4 hace el <c>economy.Spend</c> al consolidar todo al final del
    /// evento. El check de "tiene oro" se hace en este trigger contra el balance
    /// actual; si dos triggers compiten por el mismo oro en el mismo evento,
    /// los dos pueden creer que pueden gastar (race conocido — Phase 4 puede
    /// secuenciar si se vuelve un problema de balance).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class SpendGoldForComboBonus : IOnComboMatchedTrigger
    {
        [Title("Cost")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Cost;

        [Title("Bonus on Success")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Bonus;

        public void OnComboMatched(EnchantmentTriggerContext ctx)
        {
            if (ctx?.Scratch == null) return;

            int cost = Cost != null ? Cost.Read(ctx.Effect) : 0;
            if (cost <= 0)
            {
                // No-cost variant: solo suma el bonus.
                ctx.Scratch.BonusComboDamage += Bonus != null ? Bonus.Read(ctx.Effect) : 0;
                return;
            }

            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return;

            // Considera tanto el gold actual como el scratch ya acumulado por triggers previos.
            int effectiveGold = economy.CurrentGold + ctx.Scratch.BonusGold;
            if (effectiveGold < cost) return;

            ctx.Scratch.BonusGold -= cost;
            ctx.Scratch.BonusComboDamage += Bonus != null ? Bonus.Read(ctx.Effect) : 0;
        }
    }
}
