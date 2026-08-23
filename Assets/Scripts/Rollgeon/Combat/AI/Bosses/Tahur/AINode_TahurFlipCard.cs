using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>Setup de Fase 2: el cartel pasa de PIDE a LEE (la mano cantada es la que NO hay que armar) y el pozo deja de poder volver a 0.</summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurFlipCard : AIActionNode
    {
        [Tooltip("Ritmo del rastrillo a partir del volteo. El rastrillo ya corre en fase 1: acá se " +
                 "puede subir, y desde el volteo la liquidación deja de pisar el valor.")]
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
