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
    /// <para>
    /// <b>Contrato (PUL-015):</b> cada constante debe coincidir con el <c>_comboId</c> del
    /// asset <c>BaseComboSO</c> correspondiente — <c>ComboIdDropdownContractTests</c> audita
    /// la paridad en ambas direcciones. Renombrar un id se hace en asset Y constante.
    /// </para>
    /// </summary>
    public static class ComboId
    {
        /// <summary>Par — dos dados iguales.</summary>
        public const string Par = "combo.pair";

        /// <summary>Doble Par — dos grupos distintos de dos dados iguales.</summary>
        public const string DoublePair = "combo.double_pair";

        /// <summary>Trio — tres dados iguales.</summary>
        public const string Triple = "combo.trio";

        /// <summary>Escalera — cinco dados consecutivos.</summary>
        public const string Straight = "combo.ladder";

        /// <summary>Full House — un trio mas un par de distinto valor.</summary>
        public const string FullHouse = "combo.full_house";

        /// <summary>Poker — cuatro dados iguales.</summary>
        public const string Poker = "combo.poker";

        /// <summary>Generala — cinco dados iguales.</summary>
        public const string Generala = "combo.generala";

        /// <summary>
        /// Higher Number — rebrand de diseño del combo Suma X (la clase sigue siendo
        /// <c>Combo_SumaX</c>, X=4 en Warrior): al menos un dado con el valor objetivo.
        /// </summary>
        public const string HigherNumber = "combo.higher_number";

        /// <summary>Fuerza Bruta — suma los dados cuyo valor cae en la mitad superior de su propio rango.</summary>
        public const string BruteForce = "combo.brute_force";
    }
}
