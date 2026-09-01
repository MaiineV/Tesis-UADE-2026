using System;
using System.Collections;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Acción hoja: invoca un <see cref="EnemyActionBehavior"/> envolviendo el
    /// <see cref="AIContext"/> en un <see cref="EnemyAIBehaviorContext"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Behavior : AIActionNode, IAIIntentNode
    {
        [OdinSerialize, SerializeReference]
        public EnemyActionBehavior Behavior;

        public override string NodeName => Behavior != null ? Behavior.BehaviorName : "Behavior";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || Behavior == null) return AIResult.Failed;

            // Regla "sin repetir acciones": skip transparente (Succeeded, no Failed — un
            // Failed abortaría el Sequence/While padre y cortaría el turno entero).
            bool countsAsAction = !Behavior.IsEnergyBookkeeping;
            if (countsAsAction && context.HasExecuted(Behavior.BehaviorName))
                return AIResult.Succeeded;

            var bctx = new EnemyAIBehaviorContext
            {
                AI = context,
                SourceEntity = context.Self,
            };
            Behavior.Execute(bctx);
            if (countsAsAction) context.MarkExecuted(Behavior.BehaviorName);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// El golpe del behavior, si lo hay: el primer <see cref="EffDealDamage"/> cuyo daño se
        /// puede afirmar hoy. Cubre al bestiario común, cuyo ataque no es un nodo propio sino un
        /// behavior componible. Sin casillas: el target se resuelve al ejecutar, y prometer una
        /// celda acá sería adivinarla.
        /// </summary>
        public bool TryDescribeIntent(AIContext context, out AIIntent intent)
        {
            intent = default;
            if (context == null || Behavior == null || Behavior.IsEnergyBookkeeping) return false;
            if (Behavior.Effects == null) return false;

            foreach (var group in Behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var effect in group.Effects)
                {
                    if (effect is not EffDealDamage damage) continue;
                    if (!damage.TryDescribePreviewDamage(context.SelfGuid, out int amount)) continue;

                    intent = new AIIntent(AIIntentTextKeys.Attack, "Golpe", amount, damage.Kind);
                    return true;
                }
            }
            return false;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (context == null || Behavior == null) { onResult?.Invoke(AIResult.Failed); yield break; }

            bool countsAsAction = !Behavior.IsEnergyBookkeeping;
            if (countsAsAction && context.HasExecuted(Behavior.BehaviorName))
            {
                onResult?.Invoke(AIResult.Succeeded);
                yield break;
            }

            var bctx = new EnemyAIBehaviorContext
            {
                AI = context,
                SourceEntity = context.Self,
            };

            var co = Behavior.ExecuteCoroutine(bctx);
            while (co.MoveNext()) yield return co.Current;

            if (countsAsAction) context.MarkExecuted(Behavior.BehaviorName);
            onResult?.Invoke(AIResult.Succeeded);
        }
    }
}
