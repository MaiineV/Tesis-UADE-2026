using System;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Coordina las Salas de Encantamiento — escucha <c>OnRoomEntered</c>, spawnea
    /// el altar en el RewardSpawnPoint, y ofrece el flow <c>PerformEnchantment</c>
    /// que la UI consume. Mismo patrón que <c>IShopManagerService</c>.
    /// </summary>
    public interface IEnchantmentRoomService
    {
        /// <summary><c>true</c> si la room ya fue inicializada (altar instanciado).</summary>
        bool IsInitialized(Guid roomInstanceId);

        /// <summary>
        /// Callback del <see cref="EnchantmentAltarInteractable"/> cuando el
        /// player presiona interact. Resuelve el costo base y dispara
        /// <see cref="Patterns.EventName.OnEnchantmentAltarActivated"/> — la UI
        /// (Phase 6) lo consume para abrir la pantalla de selección.
        /// </summary>
        void NotifyAltarActivated(Guid roomInstanceId, string spawnPointId);

        /// <summary>
        /// Resuelve el costo del próximo uso del altar para un slot dado.
        /// Distingue first-enchant vs re-encantamiento según el config.
        /// La UI lo consume para mostrar el costo en tiempo real cuando el
        /// player hovera dados/slots.
        /// </summary>
        int ResolveCost(int bagIndex, int enchSlotIndex);

        /// <summary>
        /// Ejecuta el flow completo: cobra oro, rolea pool con compatibility +
        /// validación (intersección no-vacía), aplica encantamiento al slot.
        /// La UI llama esto al confirmar selección de dado + slot.
        /// </summary>
        EnchantmentRollResult PerformEnchantment(Guid roomInstanceId, int bagIndex, int enchSlotIndex);
    }
}
