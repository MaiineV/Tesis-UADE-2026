using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Este dado cuenta como CUALQUIER numero para propositos de combo
    /// (escaleras, trio, generala). No cambia el valor de dano.
    /// Hook: <c>OnDiceRolled</c>. Encantamiento "Comodin".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TODO Phase 4:</b> el motor de combos (<c>ContractSheet</c>) necesita
    /// saber que este dado es comodin. Requiere un flag en el resultado de roll
    /// o una estructura de datos paralela (<c>bool[] wildcards</c>) que el
    /// pipeline de <c>ComboDetectionResult</c> consuma durante el matching.
    /// </para>
    /// <para>
    /// Por ahora el trigger existe como punto de wiring para el SO; la logica
    /// real se conecta en Phase 4 cuando <c>ContractSheet</c> lea los flags
    /// de wildcard antes de evaluar combos.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class WildcardForCombo : IOnDiceRolledTrigger
    {
        public void OnDiceRolled(EnchantmentTriggerContext ctx)
        {
            // TODO Phase 4: set wildcard flag on DiceResult[BagSlotIndex] para que
            // ContractSheet lo consuma durante combo matching. El pipeline de
            // ComboDetectionResult necesita un bool[] wildcards paralelo a DiceResult.
            // Por ahora es no-op; la estructura de SO queda lista para wiring.
            Debug.Log(
                $"[WildcardForCombo] Dado en slot {ctx.Slot.BagSlotIndex} marcado como " +
                $"comodin — pending Phase 4 integration con ContractSheet.");
        }
    }
}