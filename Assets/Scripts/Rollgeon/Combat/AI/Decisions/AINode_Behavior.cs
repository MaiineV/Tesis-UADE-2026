using System;
using System.Collections;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Grid;
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

        [Tooltip("Key de Content del nombre autorado del ataque (ej. 'intent.skirmisher.x_slash'). " +
                 "Vacío = el genérico 'Golpe' (que el panel pisa con 'Disparo' para Ranged).")]
        public string IntentLabelKey;

        [Tooltip("Fallback ES del nombre autorado si la key no está en la tabla.")]
        public string IntentLabelFallback;

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
        /// behavior componible. La casilla es la del blanco que la ejecución va a resolver.
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

                    intent = string.IsNullOrEmpty(IntentLabelKey)
                        ? new AIIntent(AIIntentTextKeys.Attack, "Golpe", amount, damage.Kind,
                                       tiles: TargetCell(context, group))
                        : new AIIntent(IntentLabelKey, IntentLabelFallback, amount, damage.Kind,
                                       tiles: TargetCell(context, group));
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// La casilla del que va a recibir el golpe, resuelta con el MISMO selector que va a
        /// usar la ejecución (el override del grupo, o el del behavior; los selectores son
        /// deterministas). El blanco es una entidad y no un lugar, así que la celda no es un
        /// compromiso: se recalcula en cada hover, igual que en <see cref="AINode_RangedShot"/>.
        /// Sin grilla o sin blanco posicionado, la intención sale sin casillas.
        /// </summary>
        private GridCoord[] TargetCell(AIContext context, EffectData group)
        {
            if (context.Grid == null) return null;

            var selector = group.TargetSelector != null ? group.TargetSelector : Behavior.TargetSelector;
            var target = EnemyTargetResolver.Resolve(selector, context, context.SelfGuid);
            if (target == Guid.Empty || !context.Grid.TryGetPosition(target, out var coord)) return null;

            return new[] { coord };
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
