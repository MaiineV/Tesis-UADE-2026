using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Latch global "hay un outro de dados en curso". En Confirm, <c>OnRollResolved</c>
    /// y <c>OnBehaviorExecuted</c> disparan teardowns instantáneos el mismo frame
    /// (slots off, zona oculta, chips restaurados) que matarían el outro — los
    /// consumidores (<see cref="CombatHudZoneFlow"/>,
    /// <see cref="ActionRollExplorationVisibility"/>) chequean este estado y difieren
    /// vía <see cref="Changed"/>. Es estado y no un evento del bus porque quien llega
    /// tarde necesita poder preguntar "¿sigue pendiente?" (y <c>EventName</c> se
    /// serializa por índice — no se toca).
    /// </summary>
    public static class DiceOutroGate
    {
        public static bool OutroPending { get; private set; }

        public static event System.Action Changed;

        public static void Begin()
        {
            if (OutroPending) return;
            OutroPending = true;
            Changed?.Invoke();
        }

        public static void End()
        {
            if (!OutroPending) return;
            OutroPending = false;
            Changed?.Invoke();
        }

        // Sin domain reload (Enter Play Mode Options), los estáticos sobreviven entre
        // plays: un outro colgado dejaría la zona visible para siempre.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OutroPending = false;
            Changed = null;
        }
    }
}
