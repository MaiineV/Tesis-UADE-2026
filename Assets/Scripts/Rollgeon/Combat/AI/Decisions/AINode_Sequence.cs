using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Ejecuta hijos en orden. Corta en el primero que devuelva <see cref="AIResult.Failed"/>.
    /// Retorna <see cref="AIResult.Succeeded"/> si todos suceden, <see cref="AIResult.Failed"/>
    /// si alguno falla. TECHNICAL.md §7.5.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Sequence : AIQuestionNode
    {
        [OdinSerialize]
        public List<AIDecisionNode> Children = new List<AIDecisionNode>();

        public override string NodeName => "Sequence";

        public override AIResult Tick(AIContext context)
        {
            if (Children == null) return AIResult.Succeeded;
            foreach (var child in Children)
            {
                if (child == null) continue;
                var r = child.Tick(context);
                if (r == AIResult.Failed) return AIResult.Failed;
                if (r == AIResult.Running) return AIResult.Running;
            }
            return AIResult.Succeeded;
        }
    }
}
