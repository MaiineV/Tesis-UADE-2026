using System;
using Patterns;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Activa <see cref="RainHazardService"/> (idempotente). Pensado para envolver en
    /// <c>If(PcOwnerHpBelow) → Once(...)</c>, igual que el trigger de refuerzos — dispara
    /// una sola vez al cruzar el umbral de HP, y desde ahí la lluvia queda activa el resto
    /// de la pelea, corriendo en paralelo al boss vía su propia fuente.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ActivateRainHazard : AIActionNode
    {
        public override string NodeName => "Activate Rain Hazard";

        public override AIResult Tick(AIContext context)
        {
            if (!ServiceLocator.TryGetService<RainHazardService>(out var rain) || rain == null)
                return AIResult.Failed;

            rain.Activate();
            return AIResult.Succeeded;
        }
    }
}
