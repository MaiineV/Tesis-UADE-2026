using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;

namespace Rollgeon.ActionRolls
{
    /// <summary>
    /// Helpers compartidos para extraer el total efectivo de una tirada (formula B):
    /// si el combo matcheo, el total es <c>combo.BaseDamage</c>; sino, es la suma cruda
    /// de los pips. Reusado por <see cref="ActionRollService"/> (cuando arma el outcome)
    /// y por los effects que aceptan tirada (<c>EffForceDoor</c>, <c>EffHeal</c>) para
    /// no duplicar la regla.
    /// </summary>
    public static class ActionRollTotals
    {
        /// <summary>
        /// <para>Si <paramref name="combo"/> es un match → devuelve <c>combo.Value.BaseDamage</c>.</para>
        /// <para>Sino → fallback a <see cref="ComboCatalogSO"/> global (todos los combos
        /// del juego, no solo los del sheet del heroe). Si tampoco hay match, suma cruda.</para>
        /// </summary>
        /// <remarks>
        /// El fallback al catalog global existe para Force Door / Heal — en combate el
        /// <c>ContractSheet.MatchBest</c> solo evalua los combos que el heroe tiene en
        /// su sheet, asi que un Warrior sin Poker en sheet tirando 4 iguales caia a
        /// suma cruda. Para acciones del player que dependen de combos canonicos
        /// (no del sheet), queremos detectar TODOS los combos.
        /// </remarks>
        public static int ResolveEffectiveTotal(IReadOnlyList<int> dice, ComboDetectionResult? combo)
        {
            if (combo is { IsMatch: true } c) return c.BaseDamage;

            if (dice != null && dice.Count > 0
                && ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog)
                && catalog != null)
            {
                var fallback = ComboResolver.DetectBest(catalog, dice, out _);
                if (fallback.IsMatch) return fallback.BaseDamage;
            }

            return SumOf(dice);
        }

        public static int SumOf(IReadOnlyList<int> dice)
        {
            if (dice == null) return 0;
            int sum = 0;
            for (int i = 0; i < dice.Count; i++) sum += dice[i];
            return sum;
        }
    }
}
