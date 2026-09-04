using Rollgeon.Entities.Behaviors;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Upgrades
{
    /// <summary>Canal de upgrades que originó un dispatch de trigger. Solo debug / telemetría.</summary>
    public enum ScratchChannel
    {
        DiceEnchantment,
        ComboPassive,
        Item,
        ComboPlay,
    }

    /// <summary>
    /// <see cref="BehaviorContext"/> que los bridges de triggers (encantamientos de dados,
    /// pasivas de combo, items) cuelgan del <c>EffectContext</c> fresco de cada dispatch:
    /// transporta el scratch DEL DISPATCH EN CURSO para que efectos como
    /// <c>EffAddComboBonus</c> escriban al buffer correcto.
    /// </summary>
    /// <remarks>
    /// Un efecto nunca resuelve scratch por <c>ServiceLocator</c> — escribe al que le llega
    /// acá, sea el per-evento del canal (DiceRolled, TurnFinished, …) o el play scratch de
    /// la ventana de combo jugado. El bridge que construye el contexto decide cuál va adentro.
    /// </remarks>
    public sealed class ScratchTriggerContext : BehaviorContext
    {
        /// <summary>Scratch del dispatch en curso. Nunca null si el bridge lo armó bien.</summary>
        public EnchantmentScratch Scratch;

        /// <summary>Id del combo del evento. Null/empty en hooks no-combo.</summary>
        public string ComboId;

        /// <summary>Slot que carga el encantamiento. Null en canales sin carrier (combos / items).</summary>
        public EnchantmentSlotRef? Slot;

        /// <summary>Canal que originó el dispatch.</summary>
        public ScratchChannel Channel;

        /// <summary>Hook <c>PlayerMoved</c>: casillas de este movimiento. 0 en otros hooks.</summary>
        public int TilesTraversed;

        /// <summary>Hook <c>PlayerMoved</c>: casillas acumuladas en el turno, este movimiento incluido.</summary>
        public int TilesTraversedThisTurn;

        /// <summary>Hook <c>PlayerMoved</c>: path caminado (índice 0 = origen). Null en otros hooks.</summary>
        public System.Collections.Generic.IReadOnlyList<Rollgeon.Grid.GridCoord> Path;

        /// <summary>Hook <c>MovementDieRolled</c>: cara revelada del dado de Movimiento. 0 en otros hooks.</summary>
        public int MovementDieFace;
    }
}
