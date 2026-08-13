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
    /// <item><description><see cref="BaseDamage"/> — dano base PLANO del combo (piso). Nunca
    /// incluye valores de dados: la formula v3 ya suma las caras contribuyentes por separado
    /// (Σcaras) y sumarlas aca las contaria dos veces (Fix#0047).</description></item>
    /// <item><description><see cref="DynamicBonus"/> — parte dependiente de los dados de los
    /// combos de base dinamica (SumaX: <c>X × hits</c>; Higher Number: la cara mas alta;
    /// Fuerza Bruta: Σ de los 5 valores). <c>0</c> para combos planos. NO entra a la formula
    /// v3 (ahi las caras van via Σcaras) — existe para que la formula B legacy
    /// (<see cref="EffectiveTotal"/>) conserve el valor del combo.</description></item>
    /// <item><description><see cref="CountUsed"/> — cantidad de DADOS consumidos por el combo
    /// (no la "cuenta" ponderada del §5.1.1). Usado por counters Balatro-style (T97c) y UI de
    /// feedback. Ver plan §4.3 para la distincion semantica.</description></item>
    /// <item><description><see cref="ContributingIndices"/> — índices (relativos al array de
    /// dados recibido por <c>Detect</c>) de los dados que efectivamente formaron el combo
    /// ganador. Spec de Daño v2 (Santi): <c>multi_dmg_combo</c> se calcula SOLO sobre estos
    /// dados, no sobre todo el subset holdeado.</description></item>
    /// <item><description><see cref="ComboId"/> — id del combo que produjo el match
    /// (<c>BaseComboSO.ComboId</c>). Permite a consumers downstream (ej. la tabla de escudo
    /// por clase de <c>EffAddShield</c>) resolver datos por combo sin re-matchear.
    /// Vacío en resultados sintéticos (action rolls) y en <see cref="NoMatch"/>.</description></item>
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
        /// Dano base PLANO del combo para este match (el campo del SO, o el override de la
        /// tabla por clase). Es el termino <c>comboBase</c> de la formula v3 — las caras de
        /// los dados NO viven aca (van una sola vez via Σcaras, Fix#0047).
        /// </summary>
        public int BaseDamage { get; }

        /// <summary>
        /// Parte dinamica (dependiente de los dados) de los combos de base variable — 0 para
        /// combos planos. Solo la consume la formula B legacy via <see cref="EffectiveTotal"/>;
        /// la formula v3 de dano/escudo NO la usa (ahi las caras entran por Σcaras).
        /// </summary>
        public int DynamicBonus { get; }

        /// <summary>
        /// "Valor del combo" de la formula B (Force Door, Heal, thresholds de action rolls):
        /// <c>BaseDamage + DynamicBonus</c>. Reproduce exactamente el numero que
        /// <c>BaseDamage</c> transportaba antes de separar el doble conteo (Fix#0047).
        /// </summary>
        public int EffectiveTotal => BaseDamage + DynamicBonus;

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

        /// <summary>
        /// Id del combo que produjo el match (<c>BaseComboSO.ComboId</c>). Vacío cuando el
        /// resultado es sintético (action rolls que solo transportan EffectiveTotal) o NoMatch.
        /// </summary>
        public string ComboId { get; }

        private static readonly int[] EmptyIndices = Array.Empty<int>();

        private ComboDetectionResult(bool isMatch, string comboId, int baseDamage, int countUsed,
            IReadOnlyList<int> contributingIndices, int dynamicBonus)
        {
            IsMatch = isMatch;
            ComboId = comboId ?? string.Empty;
            BaseDamage = baseDamage;
            CountUsed = countUsed;
            ContributingIndices = contributingIndices ?? EmptyIndices;
            DynamicBonus = dynamicBonus;
        }

        /// <summary>
        /// Factory para resultado positivo con id de combo e índices de dados contribuyentes.
        /// <paramref name="dynamicBonus"/> solo lo setean los combos de base dinamica.
        /// </summary>
        public static ComboDetectionResult Match(string comboId, int baseDamage, int countUsed,
            IReadOnlyList<int> contributingIndices, int dynamicBonus = 0)
            => new ComboDetectionResult(true, comboId, baseDamage, countUsed, contributingIndices,
                dynamicBonus);

        /// <summary>
        /// Overload legacy sin id ni índices — para resultados sintéticos (action rolls) y tests
        /// de detección pura. <c>ComboId</c> queda vacío y <c>ContributingIndices</c> vacío:
        /// consumers por-combo (tabla de escudo) tratan estos resultados como "sin datos".
        /// </summary>
        public static ComboDetectionResult Match(int baseDamage, int countUsed, int dynamicBonus = 0)
            => new ComboDetectionResult(true, string.Empty, baseDamage, countUsed, EmptyIndices,
                dynamicBonus);

        /// <summary>Factory para resultado negativo (valores en 0, sin id ni índices).</summary>
        public static ComboDetectionResult NoMatch()
            => new ComboDetectionResult(false, string.Empty, 0, 0, EmptyIndices, 0);
    }
}
