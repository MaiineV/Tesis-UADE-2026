using System.Collections.Generic;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>A qué contador vuela un aporte.</summary>
    public enum BreakdownTarget
    {
        BaseN,
        /// <summary>Factor multiplicativo sobre M ("×1.5").</summary>
        MultM,
        /// <summary>Bono aditivo sobre el "1" de M ("+2"): M = ability × (1 + Σadd) × Πmult.</summary>
        AddM,
    }

    public enum BreakdownStepKind
    {
        /// <summary>El daño base del PJ (espada) vuela hacia N.</summary>
        PlayerBase,
        /// <summary>La cara de un dado contribuyente vuela hacia N.</summary>
        Die,
        /// <summary>Un encanto/proc disparado por un dado: popup desde el dado, vuela a N o M.</summary>
        DieProc,
        /// <summary>Modificador global (item/pasiva): sale del scroll derecho, vuela a N o M.</summary>
        GlobalMod,
    }

    /// <summary>Un paso de la secuencia animada de breakdown.</summary>
    public sealed class BreakdownStep
    {
        public BreakdownStepKind Kind;

        /// <summary>Slot del dado (Die/DieProc); -1 cuando no aplica.</summary>
        public int BagSlot = -1;

        /// <summary>Aporte: delta aditivo si Target=BaseN, factor si Target=MultM, bono
        /// aditivo sobre el 1 de M si Target=AddM.</summary>
        public float Amount;

        public BreakdownTarget Target = BreakdownTarget.BaseN;

        /// <summary>Id de la fuente (para logging/tests); null en PlayerBase/Die.</summary>
        public string SourceId;

        /// <summary>SO fuente (ItemSO/EnchantmentSO/...) para resolver icono; puede ser null.</summary>
        public UnityEngine.Object SourceAsset;
    }

    /// <summary>
    /// El "guion" que reproduce el director: contadores iniciales, pasos ordenados y valores
    /// finales de reconciliación. Si <see cref="Reconciled"/> es false, la suma de los pasos
    /// no cierra contra los finales (fuente sin journal): el director DEBE forzar los valores
    /// finales al terminar — el número mostrado nunca difiere del daño real.
    /// </summary>
    public sealed class BreakdownScript
    {
        public int InitialN;
        /// <summary>Arranque de M: la perilla de la habilidad (intrínseca a la acción, usualmente 1.0).</summary>
        public float InitialM;

        /// <summary>Float: el base damage override puede aportar fracción (Furia 0.25/ronda).</summary>
        public float FinalN;
        public float FinalM;
        public int FinalTotal;
        public bool Blocked;
        public bool Reconciled;

        public readonly List<BreakdownStep> Steps = new List<BreakdownStep>();
    }
}
