using System;
using System.Collections.Generic;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Curación de combo del jugador: se resuelve con la <b>misma fórmula que el daño</b>
    /// (<see cref="PlayerComboDamage.Resolve"/>, N×M), afectada por Attack (base + bonos),
    /// las caras de los dados contribuyentes, la perilla por habilidad y todos los canales
    /// de scratch — <c>BlockComboDamage</c> también bloquea curación.
    /// <c>curacion_combo_base</c> sale de <c>ContractSheet.GetHealBase</c>.
    /// </summary>
    /// <remarks>
    /// Mismo gate que <see cref="PlayerComboShield"/>: sin entrada en la HealBaseTable el
    /// combo NO cura (la tabla es opt-in por clase); si la fórmula corriera con base 0,
    /// todo combo curaría ≈ Attack.ModifiedValue. El gate decide <i>si</i> cura, la fórmula
    /// decide <i>cuánto</i>. El freno superior no es un cap: el <c>HealPipeline</c> clampea
    /// al max HP del target.
    /// </remarks>
    public static class PlayerComboHeal
    {
        public static int Resolve(Guid sourceId, int healBase,
            IReadOnlyList<ContributingDie> contributingDice, float abilityMultiplier = 1f)
            => Resolve(sourceId, healBase, contributingDice, abilityMultiplier, out _);

        /// <summary>Overload con desglose — espejo de <c>PlayerComboDamage.Resolve</c>.</summary>
        public static int Resolve(Guid sourceId, int healBase,
            IReadOnlyList<ContributingDie> contributingDice, float abilityMultiplier,
            out DamageBreakdown breakdown)
        {
            if (healBase <= 0)
            {
                // Gate: sin base no hay curación — el breakdown refleja el corte, no la fórmula.
                breakdown = new DamageBreakdown { Kind = PlayerComboFormulaKind.Heal };
                return 0;
            }
            return PlayerComboDamage.Resolve(sourceId, healBase, contributingDice,
                abilityMultiplier, PlayerComboFormulaKind.Heal, out breakdown);
        }
    }
}
