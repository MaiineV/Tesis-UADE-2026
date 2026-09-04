using System;
using System.Collections.Generic;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Oferta activa del altar: el roll pagado (palanca-primero) reveló hasta 3
    /// encantamientos, cada uno aplicable a AL MENOS un dado del bag, y el
    /// jugador todavía no confirmó (ni re-rolleó). El dado destino se elige
    /// DESPUÉS de ver la oferta. Estado del <see cref="IEnchantmentRoomService"/>
    /// — la UI lo renderiza, nunca lo muta.
    /// </summary>
    public readonly struct EnchantmentOffer
    {
        /// <summary>Room donde se pagó el roll.</summary>
        public Guid RoomInstanceId { get; }

        /// <summary>
        /// Opciones reveladas (1..3, distintas entre sí y pre-validadas: cada
        /// una pasa la coherencia contra al menos un dado del bag).
        /// </summary>
        public IReadOnlyList<EnchantmentSO> Options { get; }

        /// <summary>Oro que costó este roll.</summary>
        public int GoldPaid { get; }

        /// <summary>
        /// Set para el que se roleó (el visible en la repisa al tirar). Decide a qué dado
        /// va la confirmación: Movimiento ⇒ siempre el dado de Movimiento.
        /// </summary>
        public EnchantmentTargetSet TargetSet { get; }

        public EnchantmentOffer(Guid roomInstanceId, IReadOnlyList<EnchantmentSO> options, int goldPaid,
            EnchantmentTargetSet targetSet = EnchantmentTargetSet.CombatDice)
        {
            RoomInstanceId = roomInstanceId;
            Options = options ?? Array.Empty<EnchantmentSO>();
            GoldPaid = goldPaid;
            TargetSet = targetSet;
        }
    }

    /// <summary>
    /// Resultado de <see cref="IEnchantmentRoomService.RollOffer"/>. Si falla,
    /// NO se cobró oro (el pago solo ocurre cuando hay opciones que mostrar).
    /// </summary>
    public readonly struct EnchantmentOfferResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public EnchantmentOffer Offer { get; }

        private EnchantmentOfferResult(bool success, string error, EnchantmentOffer offer)
        {
            Success = success;
            ErrorMessage = error;
            Offer = offer;
        }

        public static EnchantmentOfferResult Ok(EnchantmentOffer offer)
            => new EnchantmentOfferResult(true, null, offer);

        public static EnchantmentOfferResult Fail(string error)
            => new EnchantmentOfferResult(false, error, default);
    }
}
