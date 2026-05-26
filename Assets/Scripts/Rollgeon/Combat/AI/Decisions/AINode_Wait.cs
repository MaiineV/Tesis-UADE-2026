using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// No-op: pasa el turno. Retorna siempre <see cref="AIResult.Succeeded"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Wait : AIActionNode
    {
        public override string NodeName => "Wait (no-op)";
        public override AIResult Tick(AIContext context) => AIResult.Succeeded;
    }
}
