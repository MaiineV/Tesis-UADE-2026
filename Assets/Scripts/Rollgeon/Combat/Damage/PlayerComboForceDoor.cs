using System;
using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Check de Forzar Puerta del jugador: la <b>misma fórmula N×M que el daño</b>
    /// (<see cref="PlayerComboDamage.Resolve"/>) comparada contra el threshold de la
    /// puerta en vez de aplicarse a un target. Reemplaza la fórmula B legacy, donde
    /// armar combo NO sumaba los pips y rendía peor que dados sueltos.
    /// </summary>
    /// <remarks>
    /// Sin combo, el caller pasa <c>comboFlatBase = 0</c> y TODOS los dados holdeados
    /// como contribuyentes (N = Attack + Σpips + bonos) — monotónico: holdear más nunca
    /// resta, y armar combo siempre mejora (suma base y habilita M &gt; 1).
    /// El <c>ForceDoorRollBonus</c> de items (Pico de Minero) NO entra acá: es flat
    /// post-multiplicador y lo suma el caller (<c>ActionRollService</c> / el fallback de
    /// <c>EffForceDoor</c>) para que "+5 a la tirada" sea literal, independiente del
    /// tuning de M.
    /// </remarks>
    public static class PlayerComboForceDoor
    {
        public static int Resolve(Guid sourceId, int comboFlatBase,
            IReadOnlyList<ContributingDie> contributingDice, float abilityMultiplier = 1f)
            => Resolve(sourceId, comboFlatBase, contributingDice, abilityMultiplier, out _);

        /// <summary>Overload con desglose — espejo de <c>PlayerComboDamage.Resolve</c>.</summary>
        public static int Resolve(Guid sourceId, int comboFlatBase,
            IReadOnlyList<ContributingDie> contributingDice, float abilityMultiplier,
            out DamageBreakdown breakdown)
        {
            return PlayerComboDamage.Resolve(sourceId, comboFlatBase, contributingDice,
                abilityMultiplier, PlayerComboFormulaKind.ForceDoor, out breakdown);
        }
    }
}
