using System;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Coordina las Salas de Encantamiento — escucha <c>OnRoomEntered</c>, spawnea
    /// el altar en el RewardSpawnPoint, y ofrece el flow de la slot machine:
    /// <see cref="RollOffer"/> (pagar → revelar 3 opciones) +
    /// <see cref="ChooseOption"/> (elegir una → se suma al dado). Mismo patrón
    /// que <c>IShopManagerService</c>.
    /// </summary>
    public interface IEnchantmentRoomService
    {
        /// <summary><c>true</c> si la room ya fue inicializada (altar instanciado).</summary>
        bool IsInitialized(Guid roomInstanceId);

        /// <summary>
        /// Callback del <see cref="EnchantmentAltarInteractable"/> cuando el
        /// player presiona interact. Resuelve el costo base y dispara
        /// <see cref="Patterns.EventName.OnEnchantmentAltarActivated"/> — la UI
        /// lo consume para abrir la pantalla.
        /// </summary>
        void NotifyAltarActivated(Guid roomInstanceId, string spawnPointId);

        /// <summary>
        /// Costo del próximo roll de la palanca: <c>base × mult^n</c>, con
        /// n = rolls totales de la run (contador global persistido — la palanca
        /// se tira ANTES de elegir dado). La UI lo muestra arriba de la palanca.
        /// </summary>
        int ResolveCost();

        /// <summary>
        /// Paga un roll y revela hasta 3 encantamientos distintos para el set visible en la
        /// repisa (<paramref name="targetSet"/>): con <see cref="EnchantmentTargetSet.CombatDice"/>
        /// cada uno es aplicable a AL MENOS un dado del bag (compatibilidad + coherencia
        /// pre-filtrada) y nunca es de Movimiento; con
        /// <see cref="EnchantmentTargetSet.MovementDie"/> son SOLO de categoría Movimiento,
        /// validados contra el dado de Movimiento. Si no hay ningún candidato válido, falla
        /// SIN cobrar. Reemplaza cualquier oferta previa.
        /// </summary>
        EnchantmentOfferResult RollOffer(Guid roomInstanceId,
            EnchantmentTargetSet targetSet = EnchantmentTargetSet.CombatDice);

        /// <summary>
        /// Confirma la elección: aplica la opción <paramref name="optionIndex"/>
        /// de la oferta activa al dado <paramref name="bagIndex"/> (se SUMA,
        /// nunca reemplaza) y limpia la oferta. Con una oferta de Movimiento el destino es
        /// siempre el dado de Movimiento (<c>EnchantmentSlotRef.MovementDieSlot</c>) y
        /// <paramref name="bagIndex"/> se ignora. Si el apply falla (ej. el dado elegido no
        /// es coherente con esa opción), la oferta se conserva.
        /// </summary>
        EnchantmentRollResult ConfirmChoice(int optionIndex, int bagIndex);

        /// <summary>Oferta activa, o null si no hay (la UI la renderiza).</summary>
        EnchantmentOffer? CurrentOffer { get; }

        /// <summary>
        /// Descarta la oferta activa (cambio de dado, cierre del panel). El oro
        /// del roll NO se devuelve — el GDD lo trata como costo hundido.
        /// </summary>
        void ClearOffer();
    }
}
