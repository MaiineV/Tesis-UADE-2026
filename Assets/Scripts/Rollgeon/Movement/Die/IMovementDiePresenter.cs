using System;
using Rollgeon.Dice;

namespace Rollgeon.Movement.Die
{
    /// <summary>
    /// Lado visual del dado de Movimiento. El servicio ya conoce la cara; el presenter
    /// la anima y avisa cuando terminó para que el servicio haga el reveal.
    /// </summary>
    public interface IMovementDiePresenter
    {
        /// <summary>
        /// Arranca la animación. Devuelve <c>false</c> si no puede presentar (view inactiva,
        /// sin slot) — el servicio entonces revela sincrónico. Si devuelve <c>true</c> DEBE
        /// invocar <paramref name="onRevealed"/> exactamente una vez.
        /// <paramref name="rangeBonus"/> es el bonus/malus de <c>MoveRange</c> del jugador
        /// (Botas/Guantelete): la view lo muestra junto a la cara al aterrizar para que el
        /// "+N" del item se LEA en la tirada, igual que un bonus en un roll de ataque.
        /// </summary>
        bool TryPresent(DiceType type, int face, int rangeBonus, Action onRevealed);

        /// <summary>Corta la animación en curso sin invocar el callback y limpia el visual.</summary>
        void Abort();
    }
}
