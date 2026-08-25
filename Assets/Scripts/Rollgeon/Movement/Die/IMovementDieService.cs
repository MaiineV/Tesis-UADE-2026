using System;
using Rollgeon.Dice;

namespace Rollgeon.Movement.Die
{
    /// <summary>
    /// Dado de Movimiento (TECHNICAL.md §6.6): entidad propia, separada por completo de los 5
    /// dados de la build (<see cref="DiceBagSO"/>). Se tira al resolver Movimiento en combate y
    /// la cara define el rango de casillas alcanzables de esa acción.
    /// </summary>
    /// <remarks>
    /// NO pasa por <see cref="IDiceRoller"/> ni por <c>IDiceThrowService</c> a propósito:
    /// el roller registrado (<c>EnchantedDiceRoller</c>) aplica encantamientos por índice de
    /// slot del bag y <c>DiceRoller</c> consume la cola de rig del DevConsole — cualquiera
    /// de los dos acoplaría el dado a la build. El rango activo se publica recién en el
    /// reveal (no al pedir la tirada) para que el hover preview no spoilee la cara.
    /// </remarks>
    public interface IMovementDieService
    {
        /// <summary>Tipo del dado en uso (override runtime, si no el de la clase, si no D4).</summary>
        DiceType CurrentType { get; }

        /// <summary>Última cara revelada (0 si nunca se tiró en este combate).</summary>
        int LastFace { get; }

        /// <summary>
        /// Override runtime del tipo (upgrades futuros). <c>null</c> vuelve al de la clase.
        /// </summary>
        void SetTypeOverride(DiceType? type);

        /// <summary>
        /// Tira el dado para <paramref name="playerGuid"/>. La cara se computa ya mismo; el
        /// reveal (callback + <c>OnMovementDieRolled</c> + rango activo) se difiere al presenter
        /// si hay uno, o es sincrónico si no.
        /// </summary>
        void Roll(Guid playerGuid, Action<int> onRevealed);

        /// <summary>Rango activo (cara revelada) para el jugador, si hay una tirada vigente.</summary>
        bool TryGetActiveRange(Guid playerGuid, out int range);

        /// <summary>
        /// Descarta la tirada vigente y cualquier reveal pendiente (cancel, fin de acción,
        /// fin de combate). Un reveal que llegue después queda como no-op.
        /// </summary>
        void ClearActiveRange();

        /// <summary>Presenter visual (HUD). <c>null</c> ⇒ reveal sincrónico.</summary>
        void SetPresenter(IMovementDiePresenter presenter);

        /// <summary>args: (playerGuid, face). Disparado en el reveal.</summary>
        event Action<Guid, int> OnRolled;

        /// <summary>Disparado por <see cref="ClearActiveRange"/> cuando había rango activo o reveal pendiente.</summary>
        event Action OnCleared;
    }
}
