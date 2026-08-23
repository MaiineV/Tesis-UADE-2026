using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>No-op si la cuenta está cancelada (rodillo roto), y siempre <see cref="AIResult.Succeeded"/>: un <c>Failed</c> acá abortaría el turno del jefe.</summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TickJackpot : AIActionNode
    {
        public override string NodeName => "Tick Jackpot Countdown";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var service = BandidaJackpotService.ResolveOrCreate();
            service.BindBoss(context.SelfGuid);
            service.Tick();

            return AIResult.Succeeded;
        }
    }
}
