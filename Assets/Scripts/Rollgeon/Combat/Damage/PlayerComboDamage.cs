using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dice;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Fórmula v2 del daño de combo del jugador (Spec de Daño, Santi):
    /// <code>
    /// dmg_base_PJ + bonos_PJ + (daño_combo_base × multi_dmg_combo) + bono_combo
    /// </code>
    /// <c>dmg_base_PJ + bonos_PJ</c> (= <c>Attack.Value</c> + resto de <c>Attack.ModifiedValue</c>)
    /// y <c>bono_combo</c> son aditivos puros — nunca se multiplican. Solo <c>daño_combo_base</c>
    /// se escala, por <c>multi_dmg_combo</c> (EV de <paramref name="contributingDice"/> / EV(d6))
    /// y por <paramref name="abilityMultiplier"/> (perilla por habilidad, ej. golpe rápido = 0.75
    /// en <c>CH_Warrior.asset</c>). <c>scratchMultiplier</c> (encantamientos Gemelo/Par-Impar) es
    /// la única excepción autorada que también multiplica ese término — no usar como plantilla.
    /// </summary>
    /// <remarks>
    /// Código puro/estático para testear la fórmula aislada. Solo aplica al ataque de combo del
    /// jugador (DamageSource.ComboValue); los enemigos usan Constant/FromReader y no pasan por acá.
    /// </remarks>
    public static class PlayerComboDamage
    {
        public static int Resolve(Guid sourceId, int comboBaseDamage,
            IReadOnlyList<DiceType> contributingDice, float abilityMultiplier = 1f)
        {
            int dmgBasePJ = 0;
            int bonosPJ = 0;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
            {
                var attack = attrs.GetAttribute<Attack>(sourceId);
                if (attack != null)
                {
                    dmgBasePJ = attack.Value;
                    bonosPJ = attack.ModifiedValue - attack.Value;
                }
            }

            int bonoCombo = 0;
            float scratchMultiplier = 1f;
            bool block = false;

            if (ServiceLocator.TryGetService<IComboPassiveService>(out var passives) && passives?.LastComboScratch != null)
            {
                bonoCombo += passives.LastComboScratch.BonusComboDamage;
                scratchMultiplier *= passives.LastComboScratch.ComboDamageMultiplier;
                block |= passives.LastComboScratch.BlockComboDamage;
            }
            if (ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants) && enchants?.LastComboScratch != null)
            {
                bonoCombo += enchants.LastComboScratch.BonusComboDamage;
                scratchMultiplier *= enchants.LastComboScratch.ComboDamageMultiplier;
                block |= enchants.LastComboScratch.BlockComboDamage;
            }

            if (block)
            {
                DamageDebugLogger.LogPlayerComposition(sourceId, dmgBasePJ, bonosPJ, comboBaseDamage,
                    1f, abilityMultiplier, scratchMultiplier, bonoCombo, blocked: true, finalBase: 0);
                return 0;
            }

            float multiDmgCombo = ComputeMultiDmgCombo(contributingDice);
            float comboTerm = comboBaseDamage * multiDmgCombo * abilityMultiplier * scratchMultiplier;
            float total = dmgBasePJ + bonosPJ + comboTerm + bonoCombo;
            int dmg = Mathf.RoundToInt(total);
            int clamped = dmg < 0 ? 0 : dmg;

            DamageDebugLogger.LogPlayerComposition(sourceId, dmgBasePJ, bonosPJ, comboBaseDamage,
                multiDmgCombo, abilityMultiplier, scratchMultiplier, bonoCombo,
                blocked: false, finalBase: clamped);

            return clamped;
        }

        /// <summary>
        /// multi_dmg_combo = EV promedio de <paramref name="contributingDice"/> / EV(d6). Público
        /// para que el preview de <c>DiceZoneView</c> muestre el mismo número que <see cref="Resolve"/>.
        /// </summary>
        public static float ComputeMultiDmgCombo(IReadOnlyList<DiceType> contributingDice)
        {
            if (contributingDice == null || contributingDice.Count == 0) return 1f;
            float sum = 0f;
            for (int i = 0; i < contributingDice.Count; i++) sum += contributingDice[i].ExpectedValue();
            return (sum / contributingDice.Count) / DiceTypeExt.BaselineExpectedValue;
        }
    }
}
