using Patterns;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Stacking GDD de los encantamientos del dado de Movimiento: varias copias del mismo
    /// encantamiento en el dado NO duplican el efecto — solo la primera copia viva (menor
    /// índice) actúa, y cada copia extra escala un parámetro (tope, duración). Helper
    /// compartido por readers y efectos para que todos cuenten igual.
    /// </summary>
    public static class MovementLaneCopies
    {
        /// <summary>
        /// Copias vivas (no tombstone) del encantamiento que ocupa <paramref name="slot"/> en
        /// el mismo dado, y si ese slot es la primera. Sin bag registrado o con slot vacío se
        /// asume una sola copia y el slot actual como primera.
        /// </summary>
        public static int Count(EnchantmentSlotRef slot, out bool isFirstCopy)
        {
            isFirstCopy = true;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var svc)
                || svc == null || svc.Bag == null)
                return 1;

            var slots = svc.Bag.GetEnchantments(slot.BagSlotIndex);
            var self = svc.Bag.GetEnchantmentAt(slot.BagSlotIndex, slot.EnchantmentSlotIndex);
            if (self == null) return 1;

            int copies = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var other = slots[i];
                if (other == null || !SameEnchantment(other, self)) continue;
                if (copies == 0) isFirstCopy = i == slot.EnchantmentSlotIndex;
                copies++;
            }
            return copies < 1 ? 1 : copies;
        }

        private static bool SameEnchantment(EnchantmentSO a, EnchantmentSO b)
        {
            if (ReferenceEquals(a, b)) return true;
            return !string.IsNullOrEmpty(a.UpgradeId) && a.UpgradeId == b.UpgradeId;
        }
    }
}
