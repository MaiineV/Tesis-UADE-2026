using System;
using Rollgeon.Dungeon;

namespace Rollgeon.Exploration
{
    /// <summary>
    /// Seam del <see cref="ExplorationController"/> para "resolver una sala de combate
    /// estándar sin pelear" (GDD: Peaje). Se consulta al entrar a una sala
    /// <c>RoomType.Combat</c> (nunca Boss) ANTES de disparar <c>OnCombatTriggered</c>.
    /// </summary>
    public interface ICombatSkipOffer
    {
        /// <summary>
        /// Devuelve <c>true</c> si el servicio se hace cargo de la sala: más tarde llamará a
        /// <paramref name="fight"/> (el jugador declinó) o la limpiará él mismo (pagó).
        /// <c>false</c> = sin oferta, el combate arranca normalmente.
        /// </summary>
        bool TryOffer(RoomInstance instance, Action fight);
    }
}
