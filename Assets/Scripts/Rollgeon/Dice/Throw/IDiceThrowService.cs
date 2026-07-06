using System;
using System.Collections.Generic;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Hub de tiradas manuales (CNF-008). Los flujos de juego (<c>CombatHandoffService</c>,
    /// <c>ActionRollService</c>) piden la tirada acá en vez de llamar a
    /// <see cref="IDiceRoller"/> directo; el servicio computa las caras AL MOMENTO del
    /// request (preserva enchantments/rig/determinismo) y decide si revelarlas al
    /// instante (Classic / sin presenter) o diferir el reveal hasta que el jugador
    /// arroje los dados y la física asiente.
    /// </summary>
    /// <remarks>
    /// El reveal (callback + <c>EventName.OnDiceRolled</c>) es el ÚNICO contrato con el
    /// resto del juego — los consumidores (DiceZoneView, paneles, zone flow) no cambian.
    /// El bookkeeping (energía/budget) queda en los callers, ANTES del request: cancelar
    /// un agarre no reembolsa nada, el throw queda pendiente y se re-agarra.
    /// </remarks>
    public interface IDiceThrowService
    {
        DiceThrowMode Mode { get; }

        /// <summary>Cambia el modo. false si hay una sesión en curso (no cambiar mid-throw).</summary>
        bool SetMode(DiceThrowMode mode);

        DiceThrowPhase Phase { get; }

        /// <summary>Sesión de throw en curso (Phase != Idle).</summary>
        bool IsBusy { get; }

        /// <summary>true si el próximo request va a diferir el reveal (modo manual + presenter vivo).</summary>
        bool WillDefer { get; }

        Guid ActivePlayerGuid { get; }

        /// <summary>Tuning compartido con los presenters (puede ser null si el bootstrap no lo seteó).</summary>
        DiceThrowSettingsSO Settings { get; }

        // ---- Lado caller (flujos de juego) ---------------------------------

        /// <summary>
        /// Tirada completa. En modo instantáneo invoca <paramref name="onRevealed"/> y
        /// emite <c>OnDiceRolled</c> sincrónicamente (orden idéntico al legacy:
        /// callback primero, evento después). En modo manual difiere ambos al settle.
        /// No-op con warning si <see cref="IsBusy"/>.
        /// </summary>
        void RequestRoll(Guid playerGuid, DiceBagSO bag, Action<int[]> onRevealed);

        /// <summary>
        /// Re-tirada respetando <paramref name="keep"/> (los kept no participan del
        /// throw y conservan su cara). Mismas semánticas de reveal que RequestRoll.
        /// </summary>
        void RequestReroll(Guid playerGuid, DiceBagSO bag, int[] previousFaces,
            bool[] keep, Action<int[]> onRevealed);

        /// <summary>
        /// Cancela la sesión SIN revelar (EndTurn / fin de combate / cancel del flujo).
        /// No emite <c>OnDiceRolled</c> ni reembolsa bookkeeping.
        /// </summary>
        void Abort();

        // ---- Lado presenter (2D/3D) ----------------------------------------

        /// <summary>Registra el presenter activo. Sin presenter, los requests resuelven
        /// instantáneos aunque el modo sea manual (anti soft-lock).</summary>
        void AttachPresenter(object owner);

        /// <summary>Desregistra. Si hay sesión en curso la aborta (presenter muerto
        /// no puede completar el reveal).</summary>
        void DetachPresenter(object owner);

        /// <summary>Qué índices participan del throw (true = vuela; held/blocked = false).</summary>
        IReadOnlyList<bool> ThrownMask { get; }

        DieThrowState GetDieState(int index);

        /// <summary>Cara precalculada del dado (modo 2D la muestra al frenar). -1 sin sesión.</summary>
        int PeekPendingFace(int index);

        bool TryGrab(int index);

        /// <summary>Devuelve los índices cuyo agarre se canceló (para tweenear la vuelta).</summary>
        IReadOnlyList<int> CancelGrab();

        /// <summary>Suelta los agarrados al vuelo. Devuelve los índices lanzados.</summary>
        IReadOnlyList<int> ReleaseGrabbed();

        /// <summary>
        /// El presenter reporta que un dado quedó quieto. En 3D pasa la cara física
        /// leída (&gt; 0); en 2D omite el parámetro (la cara ya está precalculada).
        /// </summary>
        void NotifyDieSettled(int index, int physicalFace = -1);

        /// <summary>
        /// El presenter terminó el alineado final: el servicio hace el merge de caras
        /// (2D: precalculadas; 3D: físicas salvo rig/no-d6) y dispara el reveal.
        /// </summary>
        void CompleteReveal();

        event Action<DiceThrowPhase> OnPhaseChanged;

        /// <summary>Sesión manual abierta — el presenter spawnea los visuales.</summary>
        event Action OnSessionStarted;

        /// <summary>Agarre cancelado (RMB) — el presenter tweenea los dados de vuelta.</summary>
        event Action OnGrabCancelled;

        /// <summary>Sesión abortada — el presenter destruye visuales sin revelar.</summary>
        event Action OnSessionAborted;
    }
}
