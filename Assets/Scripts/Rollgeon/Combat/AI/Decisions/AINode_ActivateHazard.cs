using System;
using Patterns;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Activa un <see cref="HazardDefinitionSO"/> vía <see cref="IHazardService"/> (idempotente).
    /// Genérico sucesor de <see cref="AINode_ActivateRainHazard"/>: la definición sale del
    /// Inspector en vez de estar hardcoded, así que un boss nuevo (o uno con varios hazards) solo
    /// necesita apuntar este nodo a otro <c>.asset</c> — nada de código nuevo. Pensado para
    /// envolver en <c>If(PcOwnerHpBelow) → Once(...)</c>, igual que el trigger de refuerzos.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ActivateHazard : AIActionNode
    {
        [Tooltip("Definición del hazard a activar. Ver HazardDefinitionSO.")]
        public HazardDefinitionSO Hazard;

        public override string NodeName => Hazard != null ? $"Activate Hazard ({Hazard.name})" : "Activate Hazard (unset)";

        public override AIResult Tick(AIContext context)
        {
            if (Hazard == null) return AIResult.Failed;
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazard) || hazard == null) return AIResult.Failed;

            hazard.Activate(Hazard);
            return AIResult.Succeeded;
        }
    }
}
