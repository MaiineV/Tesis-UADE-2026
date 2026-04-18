namespace Rollgeon.Combos
{
    /// <summary>
    /// Resultado tipado del <see cref="BaseComboSO.Detect"/>. Contrato inmutable.
    /// <para>
    /// Campos:
    /// <list type="bullet">
    /// <item><description><see cref="IsMatch"/> — <c>true</c> si el combo detecto match.</description></item>
    /// <item><description><see cref="BaseDamage"/> — dano base resultante. Para combos planos
    /// (Par, FullHouse, etc.) coincide con el campo del SO; para <c>Combo_SumaX</c> incluye la
    /// suma dinamica de los dados con valor X (ver plan §4.4).</description></item>
    /// <item><description><see cref="CountUsed"/> — cantidad de DADOS consumidos por el combo
    /// (no la "cuenta" ponderada del §5.1.1). Usado por counters Balatro-style (T97c) y UI de
    /// feedback. Ver plan §4.3 para la distincion semantica.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Por que <c>readonly struct</c></b> (plan §4.2): nombres legibles en call sites,
    /// inmutable, permite evolucion futura sin romper nombres, zero allocation (stack).
    /// </para>
    /// </summary>
    public readonly struct ComboDetectionResult
    {
        /// <summary><c>true</c> si el combo matcheo los dados recibidos.</summary>
        public bool IsMatch { get; }

        /// <summary>
        /// Dano base del combo para este match. Coincide con el <c>BaseDamage</c> del SO para
        /// combos planos; para <c>Combo_SumaX</c> es <c>BaseDamage + X * hits</c>.
        /// </summary>
        public int BaseDamage { get; }

        /// <summary>
        /// Cantidad de dados consumidos. Contrato por combo en plan §4.3 (Par=2, Trio=3, etc.).
        /// </summary>
        public int CountUsed { get; }

        private ComboDetectionResult(bool isMatch, int baseDamage, int countUsed)
        {
            IsMatch = isMatch;
            BaseDamage = baseDamage;
            CountUsed = countUsed;
        }

        /// <summary>Factory para resultado positivo.</summary>
        public static ComboDetectionResult Match(int baseDamage, int countUsed)
            => new ComboDetectionResult(true, baseDamage, countUsed);

        /// <summary>Factory para resultado negativo (valores en 0).</summary>
        public static ComboDetectionResult NoMatch()
            => new ComboDetectionResult(false, 0, 0);
    }
}
