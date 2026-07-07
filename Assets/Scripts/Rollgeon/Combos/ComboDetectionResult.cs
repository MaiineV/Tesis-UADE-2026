using System;
using System.Collections.Generic;

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
    /// <item><description><see cref="ContributingIndices"/> — índices (relativos al array de
    /// dados recibido por <c>Detect</c>) de los dados que efectivamente formaron el combo
    /// ganador. Spec de Daño v2 (Santi): <c>multi_dmg_combo</c> se calcula SOLO sobre estos
    /// dados, no sobre todo el subset holdeado.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Por que <c>readonly struct</c></b> (plan §4.2): nombres legibles en call sites,
    /// inmutable, permite evolucion futura sin romper nombres, zero allocation salvo el
    /// array de índices (solo se alloca en el path de match).
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

        /// <summary>
        /// Índices (en el array pasado a <c>Detect</c>) de los dados que formaron el combo
        /// ganador. Longitud == <see cref="CountUsed"/> para combos de conteo fijo; vacío en
        /// <see cref="NoMatch"/>.
        /// </summary>
        public IReadOnlyList<int> ContributingIndices { get; }

        private static readonly int[] EmptyIndices = Array.Empty<int>();

        private ComboDetectionResult(bool isMatch, int baseDamage, int countUsed, IReadOnlyList<int> contributingIndices)
        {
            IsMatch = isMatch;
            BaseDamage = baseDamage;
            CountUsed = countUsed;
            ContributingIndices = contributingIndices ?? EmptyIndices;
        }

        /// <summary>Factory para resultado positivo con índices de dados contribuyentes.</summary>
        public static ComboDetectionResult Match(int baseDamage, int countUsed, IReadOnlyList<int> contributingIndices)
            => new ComboDetectionResult(true, baseDamage, countUsed, contributingIndices);

        /// <summary>
        /// Overload legacy sin índices — usado solo por callers que todavía no necesitan
        /// EV de dados (ej. tests de detección pura). <c>ContributingIndices</c> queda vacío.
        /// </summary>
        public static ComboDetectionResult Match(int baseDamage, int countUsed)
            => new ComboDetectionResult(true, baseDamage, countUsed, EmptyIndices);

        /// <summary>Factory para resultado negativo (valores en 0, sin índices).</summary>
        public static ComboDetectionResult NoMatch()
            => new ComboDetectionResult(false, 0, 0, EmptyIndices);
    }
}
