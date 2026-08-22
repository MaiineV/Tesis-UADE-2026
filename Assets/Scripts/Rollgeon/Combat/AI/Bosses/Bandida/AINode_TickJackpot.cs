using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Baja un turno la cuenta regresiva del jackpot (2 → 1 → 0) y la publica para el número
    /// gigante sobre la máquina. No-op si la cuenta está cancelada (rodillo roto).
    /// </summary>
    /// <remarks>
    /// Devuelve <see cref="AIResult.Succeeded"/> incluso cuando no hay nada que bajar: un
    /// <c>Failed</c> acá abortaría el turno del jefe.
    /// </remarks>
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
