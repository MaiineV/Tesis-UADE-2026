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
    }
}
