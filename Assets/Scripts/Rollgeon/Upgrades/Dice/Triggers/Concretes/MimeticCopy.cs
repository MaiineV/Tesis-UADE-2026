using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers.Concretes
{
    /// <summary>
    /// Copia el resultado del ultimo dado rerolled en esta tirada para
    /// propositos de combo. Hook: <c>OnRollResolved</c>.
    /// Encantamiento "Mimetico".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TODO Phase 4:</b> necesita acceso al historial de rerolls (cual dado
    /// fue rerolled ultimo). Esta informacion no existe en <c>EffectContext</c>
    /// hoy. Requiere que el roll service exponga un indice del ultimo dado
    /// rerolled (ej. <c>int lastRerolledIndex</c>) o un historial completo.
    /// </para>
    /// <para>
    /// Por ahora el trigger existe como punto de wiring para el SO; la logica
    /// real se conecta en Phase 4 cuando el roll service provea el historial
    /// de rerolls.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class MimeticCopy : IOnRollResolvedTrigger
    {
        public void OnRollResolved(EnchantmentTriggerContext ctx)
        {
            // TODO Phase 4: leer el indice del ultimo dado rerolled desde el roll
            // service, copiar su face value para propositos de combo matching.
            //
            // Pseudocodigo Phase 4:
            // int lastRerolled = rollService.GetLastRerolledIndex();
            // if (lastRerolled >= 0 && lastRerolled != ctx.Slot.BagSlotIndex)
            // {
            //     int copiedFace = ctx.Effect.DiceResult[lastRerolled];
            //     // Aplicar copiedFace como valor efectivo de este dado para combos
            // }

            Debug.Log(
                $"[MimeticCopy] Roll resolved para slot {ctx.Slot.BagSlotIndex} " +
                $"— reroll history access pendiente de integracion con roll service.");
        }
    }
}