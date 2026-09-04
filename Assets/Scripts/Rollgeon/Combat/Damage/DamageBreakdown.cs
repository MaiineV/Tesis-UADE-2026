using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Desglose estructurado de una resolución de la fórmula v3 (<see cref="PlayerComboDamage"/>):
    /// todos los términos de N y M por separado, para que la UI de breakdown y el logger muestren
    /// EXACTAMENTE lo que la fórmula computó — nunca una copia paralela.
    /// Invariante: <c>Final == RoundNxM(N, M)</c>, o <c>0</c> si <see cref="Blocked"/>.
    /// </summary>
    public struct DamageBreakdown
    {
        public PlayerComboFormulaKind Kind;

        // ── Términos de N (aditivos) ──────────────────────────────────────
        /// <summary>Base del combo (0 en el fallback sin combo con detalle de bag).</summary>
        public int ComboBase;
        /// <summary>dmg_base_PJ — <c>Attack.Value</c>, o el base damage override (float:
        /// Furia Contenida acumula 0.25/ronda y la fracción viaja hasta el redondeo final).</summary>
        public float AttackBase;
        /// <summary>bonos_PJ — <c>Attack.ModifiedValue − Attack.Value</c>.</summary>
        public int AttackBonus;
        /// <summary>Σ caras de los dados contribuyentes que cuentan en N (excluye los movidos a M).</summary>
        public int FacesSum;
        /// <summary>
        /// Σ caras de los dados contribuyentes movidos a M (Fuente Mágica). NO están en
        /// <see cref="FacesSum"/>; SÍ están dentro de <see cref="ScratchMultiplierBonus"/>.
        /// </summary>
        public int MovedFacesSum;
        /// <summary>Bag slots de los dados movidos a M (subset de <see cref="Dice"/>). null = ninguno.</summary>
        public IReadOnlyList<int> DiceMovedToMultiplier;
        /// <summary>Σ BonusComboDamage de los 3 canales de scratch.</summary>
        public int AdditiveBonus;
        /// <summary>Suma de los términos aditivos — float: solo <see cref="AttackBase"/>
        /// puede aportar fracción; el redondeo único vive en <c>RoundNxM(N, M)</c>.</summary>
        public float N;

        // ── Términos de M ─────────────────────────────────────────────────
        /// <summary>Σ ComboMultiplierBonus de los 3 canales de scratch + <see cref="MovedFacesSum"/>.
        /// Entra como <c>(1 + esto)</c> al producto de M; 0 = neutro.</summary>
        public float ScratchMultiplierBonus;
        /// <summary>Producto de ComboDamageMultiplier de los 3 canales de scratch.</summary>
        public float ScratchMultiplier;
        /// <summary>Perilla por habilidad (ej. golpe rápido 0.75).</summary>
        public float AbilityMultiplier;
        /// <summary><c>(1 + ScratchMultiplierBonus) × ScratchMultiplier × AbilityMultiplier</c>
        /// (× la palanca de playtest si está prendida).</summary>
        public float M;

        // ── Resultado ─────────────────────────────────────────────────────
        public bool Blocked;
        /// <summary>Resultado final: <c>RoundNxM(N, M)</c>, o 0 si <see cref="Blocked"/>.</summary>
        public int Final;

        /// <summary>Pass-through de los dados contribuyentes (slot + cara + tipo). Puede ser null.</summary>
        public IReadOnlyList<ContributingDie> Dice;

        /// <summary>
        /// Journal agregado de atribución por fuente (encantos, pasivas, items), en orden de
        /// agregación de la fórmula: pasivas at-match → encantos at-match → canal at-played.
        /// <c>null</c> = ninguna fuente aportó. No es un timeline global.
        /// </summary>
        public IReadOnlyList<ScratchContribution> Sources;
    }
}
