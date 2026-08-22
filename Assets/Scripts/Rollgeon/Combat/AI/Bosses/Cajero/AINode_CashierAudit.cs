using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Cashier;
using Rollgeon.Feedback;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// "Arqueo de caja" (fase al 50% de HP del Cajero): guarda <see cref="TaxPercent"/> del oro del
    /// jugador en la caja del jefe, se cura con eso hasta <see cref="MaxHeal"/>, y a partir de ahí
    /// las fichas valen <see cref="ChipValueMultiplierAfterAudit"/> veces más.
    /// </summary>
    /// <remarks>
    /// Siempre Succeeded si pudo correr, incluso cobrando 0: va dentro de un <c>Once</c>, que no
    /// latchea con Failed, así que un Failed acá dejaría la Fase 2 sin anunciar para siempre.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierAudit : AIActionNode
    {
        [Tooltip("Fracción (0..1) del oro del jugador que se guarda en la caja. Ficha: 0.40.")]
        [Range(0f, 1f)]
        public float TaxPercent = 0.4f;

        [Tooltip("Tope de curación del jefe, en HP. Cura min(oro guardado, este tope). Ficha: 30.")]
        [MinValue(0)]
        public int MaxHeal = 30;

        [Tooltip("Multiplicador del valor de las fichas después del arqueo. Ficha: 2.")]
        [MinValue(1)]
        public int ChipValueMultiplierAfterAudit = 2;

        /// <summary>
        /// Event key del Animation Event del clip del jefe. Const y no campo autorable: un
        /// <c>ED_Boss_*</c> ya serializado lo deserializaría vacío.
        /// </summary>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Cashier Audit ({TaxPercent:P0} → heal ≤ {MaxHeal})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): arquea en el acto, sin
        /// gesto. Bloquear acá colgaría el runner de tests.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            Audit(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: el jefe hace el gesto de manotear la caja y el oro cambia de manos en
        /// el frame del golpe, con el turno retenido hasta que el clip termina.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (context == null || context.SelfGuid == Guid.Empty)
            {
                onResult?.Invoke(AIResult.Failed);
                yield break;
            }

            bool resolved = false;
            Action resolveOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Audit(context);
            };

            var gesture = PlayAudit(context, resolveOnce);
            while (gesture.MoveNext()) yield return gesture.Current;

            // Red de seguridad: el arqueo corre una sola vez — si se perdiera por falta de
            // presentación, el jefe se quedaría en Fase 1 el resto de la pelea.
            resolveOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        private void Audit(AIContext context)
        {
            var attrs = context.Attributes;
            if (attrs == null) ServiceLocator.TryGetService<AttributesManager>(out attrs);

            var ledger = CashierLedgerService.ResolveOrCreate();

            int collected = ledger.CollectTax(context.SelfGuid, TaxPercent);
            ledger.SetChipValueMultiplier(ChipValueMultiplierAfterAudit);

            Announce(context, collected);

            int heal = Mathf.Min(collected, MaxHeal);
            if (heal > 0) ApplyHeal(attrs, context, heal);
        }

        /// <summary>Dice el trato completo: cuánto te sacó y que vuelve si lo vencés.</summary>
        private static void Announce(AIContext context, int collected)
        {
            if (collected <= 0) return;

            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                context.PlayerGuid,
                FloatingNumberType.GoldLost,
                (float)collected,
                Vector3.zero);

            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                context.SelfGuid,
                FloatingNumberType.Status,
                VaultPromise,
                Vector3.zero);
        }

        /// <summary>Texto de la promesa de devolución. Literal: no hay tabla de localización de jefes.</summary>
        /// <remarks>
        /// "matas" y no "vencés": la pixel font del HUD (<c>m6x11plus</c>) no tiene <c>é</c> en su
        /// atlas y un glifo que falta sale como cuadradito.
        /// </remarks>
        private const string VaultPromise = "Arqueo: vuelve si lo matas";

        private static IEnumerator PlayAudit(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = BossFeedbackIds.CajeroMeleeAnim,
                StartMode = StepStartMode.Immediate,
                EndMode = StepEndMode.OnDuration,
                BlockSequence = true,
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep> { step },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool impactFired = false;
            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext())
            {
                if (!impactFired)
                {
                    var bus = FeedbackSequenceRuntime.Current;
                    if (bus != null && bus.HasFired(ImpactEventKey))
                    {
                        impactFired = true;
                        onImpact?.Invoke();
                    }
                }
                yield return wait.Current;
            }
        }

        private static void ApplyHeal(AttributesManager attrs, AIContext context, int heal)
        {
            if (attrs == null)
            {
                Debug.LogWarning("[AINode_CashierAudit] AttributesManager no disponible — se guardó " +
                                 "el oro pero el jefe no se cura.");
                return;
            }

            // Sin baseline de SelfMaxHp no se clampea: preferimos curar de más que perder la
            // curación entera del arqueo.
            int maxHp = context.SelfMaxHp > 0 ? context.SelfMaxHp : int.MaxValue;
            attrs.Modify<Health, int>(context.SelfGuid, current =>
            {
                int healed = current + heal;
                return healed > maxHp ? maxHp : healed;
            });

            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                context.SelfGuid,
                FloatingNumberType.Heal,
                (float)heal,
                Vector3.zero);
        }
    }
}
