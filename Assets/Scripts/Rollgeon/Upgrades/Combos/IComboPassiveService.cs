using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// API pública del Canal Combos — la consume la tienda (Phase 8), el damage
    /// pipeline (futuro AttackResolver) y la UI del Contrato si quiere mostrar
    /// "tenés N pasivas en este combo".
    /// </summary>
    public interface IComboPassiveService
    {
        /// <summary><c>true</c> si el state run-scoped está populado.</summary>
        bool IsReady { get; }

        /// <summary>Pasivas activas para el combo indicado. Empty si ninguna.</summary>
        IReadOnlyList<ComboPassiveSO> GetPassivesFor(string comboId);

        /// <summary>
        /// Aplica una pasiva al run (stackeable — sumás varias del mismo combo).
        /// Phase 8: la tienda invoca esto al comprar. Phase 9+: cualquier otro
        /// gate (boss reward de pasiva, etc.) llama lo mismo.
        /// </summary>
        void Apply(ComboPassiveSO passive);

        /// <summary>
        /// Bonus plano agregado sobre el daño del combo. Suma todos los
        /// <c>FlatDamageBonus.Read</c> de las pasivas que apuntan a
        /// <paramref name="comboId"/>. Cheap query — sin side effects.
        /// </summary>
        int GetBonusDamage(string comboId);

        /// <summary>
        /// Scratch resultante del último dispatch de <c>OnComboMatched</c>. Incluye
        /// los extras de los triggers (gold, shield, multiplier). Null hasta que
        /// se dispatch el primer combo en la run.
        /// </summary>
        EnchantmentScratch LastComboScratch { get; }
    }
}
