using System;
using System.Collections;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Loop node estilo "while": ejecuta <see cref="Body"/> mientras la lista AND-evaluada
    /// de <see cref="Conditions"/> sea verdadera, capped at <see cref="MaxIterations"/>.
    /// Análogo a <see cref="AINode_If"/> pero con un único child y semántica de loop.
    /// </summary>
    /// <remarks>
    /// Decisiones:
    /// <list type="bullet">
    /// <item>Lista de conditions vacía/null → permisiva (true), MaxIterations queda como único safeguard.</item>
    /// <item>Body returns Failed mid-loop → propaga Failed (la falla del child es señal de error).</item>
    /// <item>Cap alcanzado sin que la condición se vuelva false → retorna Failed + warning log
    ///       (cap señala bug de configuración, no éxito).</item>
    /// </list>
    /// Stat-mutation timing: si una acción descuenta su recurso solo al final de la animación
    /// (no sincrónicamente en Tick), la siguiente iteración puede ver el valor stale. Considerar
    /// al diseñar el árbol — preferir actions que muten sincrónicamente como child del While.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_While : AIQuestionNode
    {
        [OdinSerialize, SerializeReference]
        public BaseEnemyTargetSelector TargetSelector;

        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<BasePreCondition> Conditions = new List<BasePreCondition>();

        [OdinSerialize, SerializeReference]
        public AIDecisionNode Body;

        [Tooltip("Cap de seguridad. Si se alcanza sin que la condición se vuelva false, " +
                 "el nodo retorna Failed + warning log.")]
        [MinValue(1)]
        public int MaxIterations = 16;

        public override string NodeName => "While";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (Body == null) return AIResult.Succeeded;

            var target = EnemyTargetResolver.Resolve(TargetSelector, context, context.SelfGuid);
            var pcCtx = context.BuildPcContext(target);

            int budget = Math.Max(1, MaxIterations);
            while (budget-- > 0)
            {
                if (!BasePreCondition.EvaluateAll(Conditions, pcCtx)) return AIResult.Succeeded;
                var r = Body.Tick(context);
                if (r == AIResult.Failed) return AIResult.Failed;
            }

            Debug.LogWarning($"[AINode_While] MaxIterations={MaxIterations} reached without " +
                             "condition flipping false. Returning Failed.");
            return AIResult.Failed;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (context == null) { onResult?.Invoke(AIResult.Failed); yield break; }
            if (Body == null)    { onResult?.Invoke(AIResult.Succeeded); yield break; }

            var target = EnemyTargetResolver.Resolve(TargetSelector, context, context.SelfGuid);
            var pcCtx = context.BuildPcContext(target);

            int budget = Math.Max(1, MaxIterations);
            AIResult last = AIResult.Succeeded;
            while (budget-- > 0)
            {
                if (!BasePreCondition.EvaluateAll(Conditions, pcCtx))
                {
                    onResult?.Invoke(AIResult.Succeeded);
                    yield break;
                }

                var co = Body.TickCoroutine(context, r => last = r);
                while (co.MoveNext()) yield return co.Current;

                if (context.PendingWait != null)
                {
                    var wait = context.PendingWait;
                    context.PendingWait = null;
                    while (wait.MoveNext()) yield return wait.Current;
                }

                if (last == AIResult.Failed)
                {
                    onResult?.Invoke(AIResult.Failed);
                    yield break;
                }
            }

            Debug.LogWarning($"[AINode_While] MaxIterations={MaxIterations} reached without " +
                             "condition flipping false. Returning Failed.");
            onResult?.Invoke(AIResult.Failed);
        }
    }
}
