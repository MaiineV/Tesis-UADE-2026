using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Para propositos de escalera/straight, este dado cuenta como su valor
    /// Y como valor+1 simultaneamente. Hook: <c>OnDiceRolled</c>.
    /// Encantamiento "Escalador".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TODO Phase 4:</b> el motor de combos (<c>ContractSheet</c>) necesita
    /// saber que este dado tiene un valor secundario (face+1) para la deteccion
    /// de straights. Requiere una estructura de datos que soporte valores
    /// multiples por slot (ej. <c>int[][] secondaryValues</c> paralelo a
    /// <c>DiceResult</c>).
    /// </para>
    /// <para>
    /// Por ahora el trigger existe como punto de wiring para el SO; la logica
    /// real se conecta en Phase 4 cuando <c>ContractSheet</c> lea los valores
    /// secundarios antes de evaluar escaleras.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EscaladorStep : IOnDiceRolledTrigger
    {
        public void OnDiceRolled(EnchantmentTriggerContext ctx)
        {
            // TODO Phase 4: marcar este die slot como teniendo un valor secundario
            // (face+1) para que ContractSheet lo consuma durante straight detection.
            // Ej: ctx.Effect.SecondaryValues[ctx.Slot.BagSlotIndex] = face + 1;
            // El pipeline necesita soporte para valores multiples por slot.
            if (ctx?.Effect?.DiceResult == null) return;
            int idx = ctx.Slot.BagSlotIndex;
            if (idx < 0 || idx >= ctx.Effect.DiceResult.Count) return;

            int face = ctx.Effect.DiceResult[idx];
            Debug.Log(
                $"[EscaladorStep] Dado en slot {idx} con face {face} marcado como " +
                $"escalador (valor secundario: {face + 1}) — pending Phase 4 " +
                $"integration con ContractSheet straight detection.");
        }
    }
}