namespace Rollgeon.ActionRolls
{
    /// <summary>
    /// Resultado final del flujo. Lo recibe el callback registrado al iniciar
    /// la tirada — el effect lo usa para aplicar daño/heal/transición segun
    /// <see cref="PassedThreshold"/>.
    /// </summary>
    public struct ActionRollOutcome
    {
        /// <summary>True si el jugador canceló (sin gastar energia ni tirar).</summary>
        public bool Cancelled;

        /// <summary>Caras finales (despues del reroll si lo hubo). Null si Cancelled.</summary>
        public int[] FinalRoll;

        /// <summary>Suma cruda de los pips de FinalRoll. 0 si Cancelled.</summary>
        public int FinalSum;

        /// <summary>
        /// Total efectivo usado para comparar contra el threshold. Si la tirada
        /// matchea un combo, equivale al <c>BaseDamage</c> del combo ganador
        /// (formula B); sino, equivale a <see cref="FinalSum"/>.
        /// </summary>
        public int EffectiveTotal;

        /// <summary>True si <see cref="EffectiveTotal"/> &gt;= <c>Spec.Threshold</c>.</summary>
        public bool PassedThreshold;

        /// <summary>1 = solo tirada inicial; 2 = se uso el reroll extra.</summary>
        public int RollsUsed;

        /// <summary>ID canonico del combo matcheado (ej. "combo.poker"). Vacio si ninguno.</summary>
        public string ComboId;

        /// <summary>Nombre legible del combo matcheado para UI. Vacio si ninguno.</summary>
        public string ComboDisplayName;

        /// <summary>True si la tirada matcheo algun combo.</summary>
        public bool HasCombo;

        /// <summary>
        /// Detección REAL del combo ganador sobre el subset holdeado: ComboId, base plano
        /// (post contract-mods), CountUsed y ContributingIndices — índices relativos a
        /// <see cref="HeldDice"/>. Null si no hubo combo. Invariante:
        /// <c>Combo.Value.EffectiveTotal == EffectiveTotal</c> cuando no es null.
        /// </summary>
        public Rollgeon.Combos.ComboDetectionResult? Combo;

        /// <summary>
        /// Caras del subset holdeado que entró a la detección (excluye dados bloqueados
        /// y no holdeados). Null si Cancelled; vacío si no se holdeó nada.
        /// </summary>
        public int[] HeldDice;

        /// <summary>
        /// Mapeo subset → slot de bag, alineado 1:1 con <see cref="HeldDice"/> (misma
        /// semántica que <c>EffectContext.KeptDiceOriginalIndices</c>).
        /// </summary>
        public int[] HeldDiceOriginalIndices;
    }
}
