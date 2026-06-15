using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Fórmula unificada del daño de combo del jugador:
    /// <code>
    /// (dañoBasePJ + bonosItemsDañoPJ + (dañoBaseCombo + bonosItemsCombo)) × multiplicadorCombo
    /// </code>
    /// <list type="bullet">
    ///   <item><description><b>dañoBasePJ + bonosItemsDañoPJ</b> = <c>Attack.ModifiedValue</c> del jugador
    ///   (base del héroe + modifiers de rewards/pasivas vía <see cref="Rollgeon.Upgrades.PlayerStatGrants"/>).</description></item>
    ///   <item><description><b>dañoBaseCombo</b> = el <c>BaseDamage</c> del combo ya ajustado por la capa
    ///   de modificadores del Contrato (Boss 3) — llega resuelto en <paramref name="comboBaseDamage"/>.</description></item>
    ///   <item><description><b>bonosItemsCombo</b> = <c>BonusComboDamage</c> de las pasivas de combo + encantamientos de dado (scratch).</description></item>
    ///   <item><description><b>multiplicadorCombo</b> = <paramref name="comboMultiplier"/> (config del efecto)
    ///   × <c>ComboDamageMultiplier</c> de los scratches (ej. ParityScoreMultiplier).</description></item>
    /// </list>
    /// Si algún scratch setea <c>BlockComboDamage</c>, el daño se anula a 0.
    /// </summary>
    /// <remarks>
    /// Reúne lo que antes vivía disperso en <c>EffDealDamage</c> (que solo sumaba los bonos planos y
    /// nunca consumía el multiplicador ni el daño base del PJ). Es código puro/estático para testear
    /// la fórmula aislada. Solo aplica al ataque de combo del jugador (DamageSource.ComboValue);
    /// los enemigos usan Constant/FromReader y no pasan por acá.
    /// </remarks>
    public static class PlayerComboDamage
    {
        public static int Resolve(Guid sourceId, int comboBaseDamage, float comboMultiplier)
        {
            int playerBase = 0;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
            {
                var attack = attrs.GetAttribute<Attack>(sourceId);
                if (attack != null) playerBase = attack.ModifiedValue;
            }

            int comboBonus = 0;
            float scratchMultiplier = 1f;
            bool block = false;

            if (ServiceLocator.TryGetService<IComboPassiveService>(out var passives) && passives?.LastComboScratch != null)
            {
                comboBonus += passives.LastComboScratch.BonusComboDamage;
                scratchMultiplier *= passives.LastComboScratch.ComboDamageMultiplier;
                block |= passives.LastComboScratch.BlockComboDamage;
            }
            if (ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants) && enchants?.LastComboScratch != null)
            {
                comboBonus += enchants.LastComboScratch.BonusComboDamage;
                scratchMultiplier *= enchants.LastComboScratch.ComboDamageMultiplier;
                block |= enchants.LastComboScratch.BlockComboDamage;
            }

            if (block) return 0;

            float total = (playerBase + comboBaseDamage + comboBonus) * comboMultiplier * scratchMultiplier;
            int dmg = Mathf.RoundToInt(total);
            return dmg < 0 ? 0 : dmg;
        }
    }
}
