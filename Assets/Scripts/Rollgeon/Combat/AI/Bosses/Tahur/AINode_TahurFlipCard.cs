using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// "Se voltea la carta" — setup de Fase 2 del Tahúr (40% de HP). El cartel pasa de PIDE a LEE
    /// (la mano cantada es ahora la que NO hay que armar), entra el rastrillo (+1 ficha por ronda,
    /// sola) y el pozo deja de poder volver a 0. No cambia un solo número: cambia el puzzle.
    /// </summary>
    /// <remarks>
    /// Pensado para <c>If(PcOwnerHpBelow 0.40) → Once(FlipCard)</c>, dentro del
    /// <c>Selector[gate, Wait]</c> de aislamiento — es un one-shot: aplicado dos veces no rompe
    /// nada, pero <c>Once</c> deja explícito que el volteo pasa una vez por pelea.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurFlipCard : AIActionNode
    {
        [Tooltip("Fichas que el rastrillo suma por ronda a partir del volteo.")]
        [MinValue(0)]
        public int RakeChipsPerRound = 1;

        [Tooltip("Piso del pozo al cobrar tras el volteo: cobrar deja el pozo en 1, nunca en 0.")]
        [MinValue(0)]
        public int ChipsFloorAfterFlip = 1;

        [Tooltip("La primera liquidación después del volteo es de gracia: el canto pendiente se " +
                 "armó con las reglas viejas y castigarlo sería castigar un puzzle que cambió a " +
                 "mitad de camino.")]
        public bool GraceOnFirstSettle = true;

        public override string NodeName => "Tahúr — Flip Card (fase 2: PIDE → LEE)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            var wager = TahurWagerService.ResolveOrCreate();
            wager.FlipCard(RakeChipsPerRound, ChipsFloorAfterFlip, GraceOnFirstSettle);
            return AIResult.Succeeded;
        }
    }
}
