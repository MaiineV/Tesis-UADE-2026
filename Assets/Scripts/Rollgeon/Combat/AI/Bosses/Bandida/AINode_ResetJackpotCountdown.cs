using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Rearma la cuenta regresiva del jackpot en <see cref="Value"/> y la vuelve a poner a contar.
    /// </summary>
    /// <remarks>
    /// Va inmediatamente DESPUÉS del <c>TelegraphMark</c> del jackpot, en el mismo
    /// <c>Sequence</c>: la cuenta que dispara se rearma en el acto. Esa asimetría es de diseño —
    /// la ronda muerta solo la cobra quien rompe un rodillo (la reposición), no quien se come el
    /// jackpot. La pausa es el premio de cancelar; tanquear no la recibe.
    /// </remarks>
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
