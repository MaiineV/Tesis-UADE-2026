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
    /// Fórmula v2 del daño de combo del jugador (Spec de Daño, Santi — 2026-07-06):
    /// <code>
    /// dmg_base_PJ + bonos_PJ + (daño_combo_base × multi_dmg_combo) + bono_combo
    /// </code>
    /// <list type="bullet">
    ///   <item><description><b>dmg_base_PJ + bonos_PJ</b> — aditivo puro, <b>nunca</b> se multiplica.
    ///   <c>dmg_base_PJ = Attack.Value</c> (piso del héroe/clase); <c>bonos_PJ = Attack.ModifiedValue -
    ///   Attack.Value</c> (nivel, ítems planos, pasivas vía <see cref="Rollgeon.Upgrades.PlayerStatGrants"/>).</description></item>
    ///   <item><description><b>daño_combo_base</b> = <paramref name="comboBaseDamage"/> — ya ajustado por
    ///   la capa de modificadores del Contrato (Boss 3).</description></item>
    ///   <item><description><b>multi_dmg_combo</b> = EV promedio de <paramref name="contributingDice"/> / 3.5
    ///   (línea base d6). Se recalcula cada tirada desde SOLO los dados que formaron el combo ganador.</description></item>
    ///   <item><description><b>bono_combo</b> = <c>BonusComboDamage</c> de pasivas de combo + encantamientos de
    ///   dado. Se suma DESPUÉS del multiplicador — nunca lo escala.</description></item>
    /// </list>
    /// <para>
    /// <b>Excepciones documentadas (no reglas generales):</b>
    /// <list type="bullet">
    ///   <item><description><paramref name="abilityMultiplier"/> — perilla de diseño POR HABILIDAD
    ///   (ej. golpe rápido = 0.75, ver <c>CH_Warrior.asset</c>). No es lo mismo que multi_dmg_combo:
    ///   escala cuánto de "toda la fórmula de combo" entra esta acción puntual, no la calidad de los dados.</description></item>
    ///   <item><description><c>scratchMultiplier</c> (Gemelo/Par-Impar) — multiplica <c>daño_combo_base</c>
    ///   directamente. El spec marca esto como "señal de alarma" si se generaliza; acá es la excepción
    ///   ya autorada (D3), compuesta solo sobre el término de combo, nunca sobre dmg_base_PJ/bonos_PJ/bono_combo.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <remarks>
    /// Código puro/estático para testear la fórmula aislada. Solo aplica al ataque de combo del
    /// jugador (DamageSource.ComboValue); los enemigos usan Constant/FromReader y no pasan por acá.
    /// </remarks>
    public static class PlayerComboDamage
    {
        /// <param name="sourceId">Entidad atacante — resuelve <c>Attack</c> para dmg_base_PJ/bonos_PJ.</param>
        /// <param name="comboBaseDamage">daño_combo_base, ya resuelto por el Contrato si aplica.</param>
        /// <param name="contributingDice">Tipos de dado (no valores de cara) que formaron el combo
        /// ganador — <see cref="Rollgeon.Combos.ComboDetectionResult.ContributingIndices"/> resuelto
        /// contra el bag del jugador. Vacío/null ⇒ multi_dmg_combo neutral (×1.00).</param>
        /// <param name="abilityMultiplier">Perilla autoral por habilidad (default 1). Ver remarks.</param>
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

            if (block) return 0;

            float multiDmgCombo = ComputeMultiDmgCombo(contributingDice);
            float comboTerm = comboBaseDamage * multiDmgCombo * abilityMultiplier * scratchMultiplier;
            float total = dmgBasePJ + bonosPJ + comboTerm + bonoCombo;
            int dmg = Mathf.RoundToInt(total);
            return dmg < 0 ? 0 : dmg;
        }

        /// <summary>
        /// multi_dmg_combo = EV promedio de <paramref name="contributingDice"/> / EV(d6). Público
        /// para que la UI de preview (<c>DamageFormulaView</c> vía <c>DiceZoneView</c>) muestre
        /// exactamente el mismo número que <see cref="Resolve"/> va a aplicar, sin duplicar la
        /// fórmula. Sin info de dados ⇒ neutral (×1.00) — no penaliza ni beneficia sin evidencia
        /// real de qué dado tiró.
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
