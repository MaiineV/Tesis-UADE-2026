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
        /// </summary>
        bool TryPresent(DiceType type, int face, Action onRevealed);

        /// <summary>Corta la animación en curso sin invocar el callback y limpia el visual.</summary>
        void Abort();
    }
}
