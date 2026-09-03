using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Dados tirados que el jugador NO holdeó para la acción:
    /// <c>DiceResult.Count − KeptDice.Count</c>. Para "Dados en Reserva" (GDD: +2 al
    /// multiplicador por cada dado de la bolsa que no participa — el ×2 va en
    /// <c>EffAddComboMultiplier.ReaderScale</c>).
    /// </summary>
    /// <remarks>
    /// Decisión 2026-09-03: no existe "tirar solo parte de la bolsa", así que "no
    /// participó" = tirado y no holdeado. Sin dados holdeados (contexto sin hold) → 0, para
    /// no premiar una tirada que todavía no eligió nada.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadUnusedDiceCount : EffectIntReader
    {
        public override int Read(EffectContext context)
        {
            if (context?.DiceResult == null || context.KeptDice == null) return 0;
            return Math.Max(0, context.DiceResult.Count - context.KeptDice.Count);
        }
    }
}
