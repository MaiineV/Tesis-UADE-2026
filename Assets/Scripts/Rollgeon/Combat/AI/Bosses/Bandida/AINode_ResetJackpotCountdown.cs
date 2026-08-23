using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Va inmediatamente después del <c>TelegraphMark</c> del jackpot, en el mismo <c>Sequence</c>:
    /// la cuenta que dispara se rearma en el acto.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ResetJackpotCountdown : AIActionNode
    {
        [Tooltip("Valor con el que arranca la cuenta de nuevo. 2 = dos rondas de aviso.")]
        [MinValue(0)]
        public int Value = 2;

        public override string NodeName => $"Reset Jackpot Countdown ({Value})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var service = BandidaJackpotService.ResolveOrCreate();
            service.BindBoss(context.SelfGuid);
            service.ResetCountdown(Value);

            return AIResult.Succeeded;
        }
    }
}
