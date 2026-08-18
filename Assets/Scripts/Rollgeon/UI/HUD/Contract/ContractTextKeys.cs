namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Claves de la tabla UI para el drawer de contrato y para las marcas de regla que los
    /// jefes le dejan encima.
    /// </summary>
    public static class ContractTextKeys
    {
        /// <summary>Columna de la mano de ejemplo.</summary>
        public const string HeaderExample = "contract.header.example";

        /// <summary>Columna del nombre del combo.</summary>
        public const string HeaderName = "contract.header.name";

        /// <summary>Columna del daño base.</summary>
        public const string HeaderDamage = "contract.header.damage";

        /// <summary>Columna de la descripción (solo la tabla de selección de clase).</summary>
        public const string HeaderDescription = "contract.header.description";

        // Los textos de marca se concatenan con el número o el nombre del combo destino, sin
        // placeholders: viajan por la tabla UI y un {0} mal autorado ahí tira en pantalla.

        /// <summary>Título de la planilla persistente.</summary>
        public const string RuleBoardTitle = "contract.rule.board_title";

        /// <summary>Badge de combo prohibido (paga 0).</summary>
        public const string RuleForbidden = "contract.rule.forbidden";

        /// <summary>Badge de combo bloqueado. Se le concatena la cuenta de turnos.</summary>
        public const string RuleBlocked = "contract.rule.blocked";

        /// <summary>Badge de combo corrido. Se le concatena el nombre de la fila destino.</summary>
        public const string RuleShifted = "contract.rule.shifted";
    }
}
