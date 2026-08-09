using System;
using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Fórmula v3 del escudo de combo del jugador (decisión de diseño 2026-08-06, revierte la
    /// separación de la Spec v2): el escudo se resuelve con la <b>misma fórmula que el daño</b>
    /// (<see cref="PlayerComboDamage.Resolve"/>), afectado por Attack (base + bonos), el multi
    /// de dados, la perilla por habilidad y todos los canales de scratch (pasivas,
    /// encantamientos, items at-played) — <c>BlockComboDamage</c> también bloquea escudo.
    /// <c>escudo_combo_base</c> sigue saliendo de <c>ContractSheet.GetShieldBase</c>.
    /// </summary>
    /// <remarks>
    /// Única divergencia con el daño: el gate de base 0. Sin entrada en la ShieldBaseTable el
    /// combo NO genera escudo (la tabla es opt-in por clase); si la fórmula corriera con base 0,
    /// todo combo daría escudo ≈ Attack.ModifiedValue. El gate decide <i>si</i> hay escudo, la
    /// fórmula decide <i>cuánto</i>. Ya no hay cap — el freno anti-inmunidad es el reset por
    /// turno (<c>ShieldResetHandler</c>) más la escala ×10 del daño enemigo.
    /// </remarks>
    public static class PlayerComboShield
    {
        public static int Resolve(Guid sourceId, int shieldBase,
            IReadOnlyList<ContributingDie> contributingDice, float abilityMultiplier = 1f)
        {
            if (shieldBase <= 0) return 0;
            return PlayerComboDamage.Resolve(sourceId, shieldBase, contributingDice,
                abilityMultiplier, PlayerComboFormulaKind.Shield);
        }
    }
}
