using System;
using System.Collections;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Acción hoja: invoca un <see cref="EnemyActionBehavior"/> envolviendo el
    /// <see cref="AIContext"/> en un <see cref="EnemyAIBehaviorContext"/>. Reemplaza
    /// a <c>AINode_Attack</c>: la "acción de atacar" pasa a ser un behavior con un
    /// <c>EffDealDamage</c> en su pipeline.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Behavior : AIActionNode
    {
        [OdinSerialize, SerializeReference]
        public EnemyActionBehavior Behavior;

        public override string NodeName => Behavior != null ? Behavior.BehaviorName : "Behavior";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || Behavior == null) return AIResult.Failed;

            var bctx = new EnemyAIBehaviorContext
            {
                AI = context,
                SourceEntity = context.Self,
            };
            Behavior.Execute(bctx);
            return AIResult.Succeeded;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (context == null || Behavior == null) { onResult?.Invoke(AIResult.Failed); yield break; }

            var bctx = new EnemyAIBehaviorContext
            {
                AI = context,
                SourceEntity = context.Self,
            };

            var co = Behavior.ExecuteCoroutine(bctx);
            while (co.MoveNext()) yield return co.Current;

            onResult?.Invoke(AIResult.Succeeded);
        }
    }
}
