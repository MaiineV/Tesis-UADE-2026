using System;

namespace Rollgeon.Player
{
    /// <summary>
    /// [STUB] — §G minimal interface.
    /// Servicio global que expone la <c>Entity</c> del jugador activo. Registrado
    /// como <c>ServiceScope.Global</c> en el bootstrap (§1.1.1).
    /// <para>
    /// Este stub define la <b>superficie minima</b> que UI#0095a consume: solo
    /// <see cref="PlayerGuid"/> para filtrar eventos por entidad. El contrato
    /// completo de TECHNICAL.md §17.G (<c>CurrentEntity</c>, <c>RunId</c>,
    /// <c>SetPlayer</c>, <c>ClearPlayer</c>, <c>OnPlayerSet</c>,
    /// <c>OnPlayerCleared</c>) lo aterrizara F#0008 cuando mergee. Este worktree
    /// no provee implementacion — solo la interface.
    /// </para>
    /// </summary>
    /// <remarks>
    /// [STUB] — replace with upstream when F#0008 merges.
    /// Si no hay <see cref="IPlayerService"/> registrado al momento del <c>Bind</c>,
    /// <see cref="Rollgeon.UI.Screens.ExplorationHUDView"/> degrada gracefully
    /// (warning + <c>Guid.Empty</c> fallback, ver plan §2.3).
    /// </remarks>
    public interface IPlayerService
    {
        /// <summary>
        /// GUID del jugador activo. <see cref="Guid.Empty"/> si no hay player seteado
        /// (HUD se pushea antes del spawn, o despues de <c>OnRunEnd</c>).
        /// </summary>
        Guid PlayerGuid { get; }
    }
}
