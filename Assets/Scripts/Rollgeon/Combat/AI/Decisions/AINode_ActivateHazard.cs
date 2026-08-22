using System;
using Patterns;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Activa un <see cref="HazardDefinitionSO"/> vía <see cref="IHazardService"/> (idempotente).
    /// La definición sale del Inspector, así que un boss con varios hazards sólo necesita apuntar
    /// otra instancia del nodo a otro <c>.asset</c>.
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
