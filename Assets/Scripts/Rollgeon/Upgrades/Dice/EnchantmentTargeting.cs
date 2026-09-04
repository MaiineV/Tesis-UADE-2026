namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Qué dados muestra la repisa del altar y, por lo tanto, a qué dado va a parar la
    /// oferta de la palanca. La UI elige el set con el carousel ANTES de tirar.
    /// </summary>
    public enum EnchantmentTargetSet
    {
        /// <summary>Los 5 dados de combate del <c>DiceBagSO</c>.</summary>
        CombatDice = 0,

        /// <summary>El dado de Movimiento (§6.6), carril <see cref="EnchantmentSlotRef.MovementDieSlot"/>.</summary>
        MovementDie = 1,
    }

    /// <summary>
    /// Regla GDD ("Listado encantamientos", regla especial del dado de movimiento): los
    /// encantamientos de categoría 🗺️ Movimiento SOLO pueden ir al dado de Movimiento, y
    /// ninguna otra categoría puede ir ahí. Es el único lugar con la regla — el altar, el
    /// <c>DiceEnchantmentService</c> y la DevConsole la consultan acá.
    /// </summary>
    public static class EnchantmentTargeting
    {
        /// <summary>
        /// <c>true</c> si <paramref name="ench"/> puede aplicarse a un dado del set. Un
        /// encantamiento sin categoría (<c>None</c>, típico de tests) cuenta como de combate.
        /// </summary>
        public static bool AppliesTo(EnchantmentSO ench, EnchantmentTargetSet set)
        {
            if (ench == null) return false;
            bool movement = ench.Category == EnchantmentCategory.Movimiento;
            return set == EnchantmentTargetSet.MovementDie ? movement : !movement;
        }

        /// <summary>Set al que pertenece un índice del espacio de <c>BagSlotIndex</c>.</summary>
        public static EnchantmentTargetSet SetForIndex(int bagIndex)
        {
            return bagIndex == EnchantmentSlotRef.MovementDieSlot
                ? EnchantmentTargetSet.MovementDie
                : EnchantmentTargetSet.CombatDice;
        }
    }
}
