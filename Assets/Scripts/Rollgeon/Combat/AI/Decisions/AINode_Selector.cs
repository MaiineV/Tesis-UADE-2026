using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Intenta hijos en orden hasta que uno devuelva <see cref="AIResult.Succeeded"/>.
    /// Corto-circuito al primer éxito. Retorna <see cref="AIResult.Failed"/> si todos fallan.
    /// TECHNICAL.md §7.5.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Selector : AIQuestionNode
    {
        [OdinSerialize]
        public List<AIDecisionNode> Children = new List<AIDecisionNode>();

        public override string NodeName => "Selector";

        public override AIResult Tick(AIContext context)
        {
            if (Children == null) return AIResult.Failed;
            foreach (var child in Children)
            {
                if (child == null) continue;
                var r = child.Tick(context);
                if (r == AIResult.Succeeded) return AIResult.Succeeded;
                if (r == AIResult.Running) return AIResult.Running;
            }
            return AIResult.Failed;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (Children == null) { onResult?.Invoke(AIResult.Failed); yield break; }

            foreach (var child in Children)
            {
                if (child == null) continue;

                AIResult childResult = AIResult.Failed;
                var co = child.TickCoroutine(context, r => childResult = r);
                while (co.MoveNext()) yield return co.Current;

                if (childResult == AIResult.Succeeded) { onResult?.Invoke(AIResult.Succeeded); yield break; }
            }
            onResult?.Invoke(AIResult.Failed);
        }
    }
}
