namespace Rollgeon.Combos
{
    /// <summary>
    /// Constantes centrales con los IDs canonicos de los combos del Sprint #97 (Guerrero).
    /// Formato per TECHNICAL.md §12.6 ActionId naming: <c>combo.&lt;snake_case&gt;</c>.
    /// <para>
    /// Usado por los concretos, los tests y downstream (T97b <c>ContractWarriorSO</c>) para
    /// referenciar los IDs desde codigo sin magic strings. Los <c>.asset</c> siguen usando
    /// <c>[ValueDropdown]</c> en inspector — estas constantes son la fuente unica en codigo.
    /// </para>
    /// </summary>
    public static class ComboId
    {
        /// <summary>Par — dos dados iguales.</summary>
        public const string Par = "combo.par";

        /// <summary>Doble Par — dos grupos distintos de dos dados iguales.</summary>
        public const string DoublePair = "combo.double_pair";

        /// <summary>Trio — tres dados iguales.</summary>
        public const string Triple = "combo.triple";

        /// <summary>Escalera — cinco dados consecutivos.</summary>
        public const string Straight = "combo.straight";

        /// <summary>Full House — un trio mas un par de distinto valor.</summary>
        public const string FullHouse = "combo.full_house";

        /// <summary>Poker — cuatro dados iguales.</summary>
        public const string Poker = "combo.poker";

        /// <summary>Generala — cinco dados iguales.</summary>
        public const string Generala = "combo.generala";

        /// <summary>Suma X — combo parametrizado por valor objetivo (X=4 en Warrior).</summary>
        public const string SumX = "combo.sum_x";
    }
}
