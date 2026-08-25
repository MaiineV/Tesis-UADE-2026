using System;
using System.Collections.Generic;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Resultado de <c>IEnchantmentRoomService.ChooseOption</c>: el encantamiento
    /// elegido de la oferta, el oro que costó el roll que la reveló, y el preview
    /// de caras resultantes para la UI.
    /// </summary>
    public readonly struct EnchantmentRollResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }
        public EnchantmentSO RolledEnchantment { get; }
        public int GoldPaid { get; }
        public IReadOnlyCollection<int> ProjectedFaces { get; }

        private EnchantmentRollResult(bool success, string error, EnchantmentSO rolled, int gold, IReadOnlyCollection<int> faces)
        {
            Success = success;
            ErrorMessage = error;
            RolledEnchantment = rolled;
            GoldPaid = gold;
            ProjectedFaces = faces ?? Array.Empty<int>();
        }

        public static EnchantmentRollResult Ok(EnchantmentSO rolled, int goldPaid, IReadOnlyCollection<int> projectedFaces)
            => new EnchantmentRollResult(true, null, rolled, goldPaid, projectedFaces);

        public static EnchantmentRollResult Fail(string error)
            => new EnchantmentRollResult(false, error, null, 0, null);
    }
}
