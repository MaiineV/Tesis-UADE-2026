using UnityEngine;

namespace Rollgeon.Input
{
    /// <summary>
    /// Latch por frame para el click derecho: un consumidor que ya lo manejó este
    /// frame (ej. el cancel de agarre de los throw presenters) lo "claimea" y el
    /// router global (<see cref="RightClickCancelController"/>) no lo double-handlea.
    /// Mismo patrón que <c>GameplayHotkeyService.ConsumeFrame</c> — se auto-resetea
    /// por <c>Time.frameCount</c>, sin leaks entre play sessions.
    /// </summary>
    public static class RightClickClaim
    {
        private static int _claimedFrame = -1;

        public static void Claim() => _claimedFrame = Time.frameCount;

        public static bool WasClaimedThisFrame => _claimedFrame == Time.frameCount;
    }
}
