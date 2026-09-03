using System.Collections.Generic;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Cuántas tiradas consecutivas lleva cada dado del bag GUARDADO (sin relanzar)
    /// dentro de la mano actual. Alimenta a "Ancla" (+5 por tirada guardada) y a
    /// cualquier encantamiento que premie la paciencia.
    /// </summary>
    /// <remarks>
    /// El productor es <c>CombatHandoffService</c>: cada reroll de combate reporta la
    /// máscara <c>keep</c> (true = el dado se quedó). Un roll fresco (primera tirada de
    /// la mano, reroll forzado de Torpe) y el inicio/fin de combate resetean todo.
    /// </remarks>
    public interface IDiceHoldStreakService
    {
        /// <summary>Tiradas consecutivas que el dado <paramref name="bagSlot"/> lleva guardado. 0 si nunca / fuera de rango.</summary>
        int GetStreak(int bagSlot);

        /// <summary>Roll fresco de toda la mano: ningún dado viene guardado.</summary>
        void OnFreshRoll();

        /// <summary>Reroll con máscara: los <c>keep=true</c> suman una tirada guardada; el resto vuelve a 0.</summary>
        void OnReroll(IReadOnlyList<bool> keep);
    }
}
