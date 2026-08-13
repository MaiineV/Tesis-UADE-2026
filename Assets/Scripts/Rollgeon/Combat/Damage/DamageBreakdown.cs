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
        /// <summary>dmg_base_PJ — <c>Attack.Value</c>.</summary>
        public int AttackBase;
        /// <summary>bonos_PJ — <c>Attack.ModifiedValue − Attack.Value</c>.</summary>
        public int AttackBonus;
        /// <summary>Σ caras de los dados contribuyentes.</summary>
        public int FacesSum;
        /// <summary>Σ BonusComboDamage de los 3 canales de scratch.</summary>
        public int AdditiveBonus;
        public int N;

        // ── Términos de M (multiplicativos) ───────────────────────────────
        /// <summary>Producto de ComboDamageMultiplier de los 3 canales de scratch.</summary>
        public float ScratchMultiplier;
        /// <summary>Perilla por habilidad (ej. golpe rápido 0.75).</summary>
        public float AbilityMultiplier;
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
