using System;
using Rollgeon.Combat.BossHand;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Le habilita (o le quita) el reroll a la mano de dados del propio boss —
    /// <see cref="IBossDiceHandService.SetRerollsPerRound"/>.
    /// </summary>
    /// <remarks>
    /// El flag vive en el servicio (run-scoped), así que aplicarlo una vez alcanza y re-aplicarlo es
    /// idempotente.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SetHandReroll : AIActionNode
    {
        [Tooltip("Cuántas veces re-tira los dados que no le sirven por tirada. 0 = sin reroll.")]
        [MinValue(0)]
        public int RerollsPerRound = 1;

        public override string NodeName => $"Set Hand Reroll ({RerollsPerRound}/roll)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            BossDiceHandService.ResolveOrCreate().SetRerollsPerRound(context.SelfGuid, RerollsPerRound);
            return AIResult.Succeeded;
        }
    }
}
