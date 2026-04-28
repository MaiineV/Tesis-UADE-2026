using System;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

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

            // Las excepciones del behavior burbujean al outer try/catch de
            // TreeDrivenEnemyAI; este nodo no las traga.
            var bctx = new EnemyAIBehaviorContext
            {
                AI = context,
                SourceEntity = context.Self,
            };
            Behavior.Execute(bctx);
            return AIResult.Succeeded;
        }
    }
}
