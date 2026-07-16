using System;
using System.Collections.Generic;

namespace Rollgeon.Combat.Resume
{
    /// <summary>
    /// Seam consultado por <c>CombatEnterState.Enter</c> al arrancar un combate: si hay un
    /// snapshot de combate stageado desde el save (Feature#0028 Fase 3), restaura la cola de
    /// turnos exacta + energía + buffs en vez del <c>BuildForCombat</c> fresco. La impl vive
    /// en la capa Run (<c>CombatResumeService</c>) y también es el <c>ISaveable</c> del estado.
    /// </summary>
    public interface ICombatResumeCoordinator
    {
        /// <summary>
        /// One-shot: si el snapshot stageado está activo y su celda matchea la sala actual,
        /// restaura el estado de turno sobre <paramref name="turnOrder"/> (filtrando la cola a
        /// <paramref name="liveParticipants"/>) y devuelve <c>true</c> — el caller NO debe
        /// llamar <c>BuildForCombat</c>. Consume el snapshot. <c>false</c> ⇒ combate normal.
        /// <para>
        /// <paramref name="livePlayerId"/> es el GUID vivo del player: el guardado se re-mapea
        /// a este, así el resume no depende de la preservación cross-sesión del GUID del player.
        /// </para>
        /// </summary>
        bool TryBeginResume(TurnOrderService turnOrder, IReadOnlyList<Guid> liveParticipants, Guid livePlayerId);
    }
}
