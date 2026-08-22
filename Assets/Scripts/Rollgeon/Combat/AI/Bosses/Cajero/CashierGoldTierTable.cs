using System.Collections.Generic;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Resolución pura de escalones: dado el oro del jugador, los escalones que le sumó el
    /// rastrillo (el reloj del jefe) y los que le descontó el soborno, devuelve el
    /// <see cref="CashierGoldTier"/> que corresponde. Sin estado y sin servicios.
    /// </summary>
    /// <remarks>
    /// No asume que la lista venga ordenada: rankea por <see cref="CashierGoldTier.MinGold"/>
    /// ascendente (empate ⇒ orden de autoría), y sobre ese ranking "bajar un escalón" está bien
    /// definido. El umbral es inclusive (<c>gold &gt;= MinGold</c>); el oro por debajo del escalón
    /// más bajo cae igual en él y el descuento del soborno se clampea a 0. El rastrillo se topea al
    /// escalón más caro ANTES de restar el soborno: al revés, con el reloj sumando +1 cada N rondas
    /// el descuento sería invisible desde la primera decena de rondas.
    /// </remarks>
    public static class CashierGoldTierTable
    {
        /// <summary>
        /// Escalón resuelto, o <c>null</c> si la tabla no tiene ninguno usable.
        /// <paramref name="rank"/> devuelve la posición (0-based) en el ranking por MinGold,
        /// útil para logging/debug y para los tests.
        /// </summary>
        public static CashierGoldTier Resolve(
            IReadOnlyList<CashierGoldTier> tiers, int gold, int stepDown, int stepUp, out int rank)
        {
            rank = -1;
            var ranked = Rank(tiers);
            if (ranked.Count == 0) return null;

            int hit = 0;
            for (int i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].MinGold <= gold) hit = i;
            }

            int raised = hit + (stepUp < 0 ? 0 : stepUp);
            if (raised > ranked.Count - 1) raised = ranked.Count - 1;

            int discounted = raised - (stepDown < 0 ? 0 : stepDown);
            if (discounted < 0) discounted = 0;

            rank = discounted;
            return ranked[discounted];
        }

        /// <summary>Overload sin rastrillo (<c>stepUp = 0</c>) — el escalón sale sólo del oro y
        /// del soborno.</summary>
        public static CashierGoldTier Resolve(
            IReadOnlyList<CashierGoldTier> tiers, int gold, int stepDown, out int rank)
            => Resolve(tiers, gold, stepDown, 0, out rank);

        /// <summary>Overload sin <c>rank</c> ni rastrillo, para los call sites que solo quieren
        /// el escalón.</summary>
        public static CashierGoldTier Resolve(IReadOnlyList<CashierGoldTier> tiers, int gold, int stepDown)
            => Resolve(tiers, gold, stepDown, 0, out _);

        /// <summary>Overload sin <c>rank</c>, con rastrillo.</summary>
        public static CashierGoldTier Resolve(
            IReadOnlyList<CashierGoldTier> tiers, int gold, int stepDown, int stepUp)
            => Resolve(tiers, gold, stepDown, stepUp, out _);

        /// <summary>
        /// Escalones no-nulos ordenados por <see cref="CashierGoldTier.MinGold"/> ascendente.
        /// Insertion sort a propósito: la tabla tiene 3 entradas y necesitamos estabilidad
        /// (empates de MinGold conservan el orden de autoría), que <c>List.Sort</c> no garantiza.
        /// </summary>
        public static List<CashierGoldTier> Rank(IReadOnlyList<CashierGoldTier> tiers)
        {
            var ranked = new List<CashierGoldTier>();
            if (tiers == null) return ranked;

            foreach (var tier in tiers)
            {
                if (tier == null) continue;

                int at = ranked.Count;
                while (at > 0 && ranked[at - 1].MinGold > tier.MinGold) at--;
                ranked.Insert(at, tier);
            }
            return ranked;
        }
    }
}
