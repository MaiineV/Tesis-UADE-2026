using System.Collections.Generic;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// No asume que la lista venga ordenada: rankea por <see cref="CashierGoldTier.MinGold"/>
    /// ascendente (empate ⇒ orden de autoría). El umbral es inclusive; el oro por debajo del escalón
    /// más bajo cae igual en él y el soborno se clampea a 0. El rastrillo se topea al escalón más
    /// caro ANTES de restar el soborno: al revés, el descuento sería invisible enseguida.
    /// </summary>
    public static class CashierGoldTierTable
    {
        /// <summary>Escalón resuelto, o <c>null</c> si la tabla no tiene ninguno usable. <paramref name="rank"/> es la posición (0-based) en el ranking por MinGold.</summary>
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

        /// <summary>Overload sin rastrillo (<c>stepUp = 0</c>).</summary>
        public static CashierGoldTier Resolve(
            IReadOnlyList<CashierGoldTier> tiers, int gold, int stepDown, out int rank)
            => Resolve(tiers, gold, stepDown, 0, out rank);

        public static CashierGoldTier Resolve(IReadOnlyList<CashierGoldTier> tiers, int gold, int stepDown)
            => Resolve(tiers, gold, stepDown, 0, out _);

        public static CashierGoldTier Resolve(
            IReadOnlyList<CashierGoldTier> tiers, int gold, int stepDown, int stepUp)
            => Resolve(tiers, gold, stepDown, stepUp, out _);

        /// <summary>Insertion sort a propósito: 3 entradas y hace falta estabilidad (empates de MinGold conservan el orden de autoría), que <c>List.Sort</c> no da.</summary>
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
