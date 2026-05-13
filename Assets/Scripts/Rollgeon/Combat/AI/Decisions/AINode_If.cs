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
    /// Ramifica según una lista AND-evaluada de <see cref="BasePreCondition"/>. Reemplaza
    /// al monolítico <c>AICondition</c> usando el catálogo central de PC.
    /// TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// El target del context PC se resuelve via <see cref="TargetSelector"/>; null cae
    /// a <see cref="TargetSelector_AlwaysPlayer"/>. Lista de PC vacía/null = pasa
    /// (equivale a "no hay condición" → siempre Then).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_If : AIQuestionNode
    {
        [OdinSerialize, SerializeReference]
        public BaseEnemyTargetSelector TargetSelector;

        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<BasePreCondition> Conditions = new List<BasePreCondition>();

        [OdinSerialize] public AIDecisionNode Then;
        [OdinSerialize] public AIDecisionNode Else;

        public override string NodeName => "If";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            var target = EnemyTargetResolver.Resolve(TargetSelector, context, context.SelfGuid);
            var pcCtx = context.BuildPcContext(target);
            bool pass = BasePreCondition.EvaluateAll(Conditions, pcCtx);

            var branch = pass ? Then : Else;
            return branch?.Tick(context) ?? AIResult.Failed;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (context == null) { onResult?.Invoke(AIResult.Failed); yield break; }

            var target = EnemyTargetResolver.Resolve(TargetSelector, context, context.SelfGuid);
            var pcCtx = context.BuildPcContext(target);
            bool pass = BasePreCondition.EvaluateAll(Conditions, pcCtx);

            var branch = pass ? Then : Else;
            if (branch == null) { onResult?.Invoke(AIResult.Failed); yield break; }

            var co = branch.TickCoroutine(context, onResult);
            while (co.MoveNext()) yield return co.Current;
        }
    }
}
