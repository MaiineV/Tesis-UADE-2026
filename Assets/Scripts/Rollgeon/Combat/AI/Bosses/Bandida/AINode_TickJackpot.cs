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
    /// Va suelto en el <c>Sequence</c> raíz, antes del pool de acción: devuelve siempre
    /// <see cref="AIResult.Succeeded"/> para no abortar el turno del jefe. Quien decide si en 0 se
    /// marca el jackpot es el <c>Selector</c> del pool vía <c>PcJackpotCountdown</c> — este nodo
    /// solo lleva la cuenta.
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
