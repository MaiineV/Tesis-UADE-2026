using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Pisa el delay de reposición de los rodillos (Fase 2 lo baja a 1 turno).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SetReelRespawnDelay : AIActionNode
    {
        [Tooltip("Turnos del jefe que tarda un rodillo roto en volver, de acá en adelante.")]
        [MinValue(0)]
        public int Value = 1;

        public override string NodeName => $"Set Reel Respawn Delay ({Value})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var service = BandidaJackpotService.ResolveOrCreate();
            service.BindBoss(context.SelfGuid);
            service.SetRespawnDelay(Value);

            return AIResult.Succeeded;
        }
    }
}
