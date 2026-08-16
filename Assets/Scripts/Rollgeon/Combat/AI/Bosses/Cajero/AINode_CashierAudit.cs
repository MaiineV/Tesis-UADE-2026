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
    /// <para>
    /// <b>No es un robo, es un secuestro.</b> El oro no desaparece: queda en
    /// <c>ICashierLedgerService.VaultedGold</c> y vuelve completo al jugador cuando el jefe muere
    /// (el servicio escucha <c>OnEntityDestroyed</c>). Si el jugador muere primero, gana la banca.
    /// Es el único punto del kit del Cajero que escribe oro del jugador, y está en la ficha.
    /// </para>
    /// <para>
    /// <b>Siempre Succeeded si pudo correr</b> (incluso cobrando 0 porque el jugador está seco):
    /// va envuelto en <c>Once → Sequence[Audit, ApplyStatModifier]</c>, y un Failed acá abortaría
    /// la secuencia y dejaría la Fase 2 sin su feedback — el jefe se quedaría sin anunciar el
    /// cambio para siempre porque <c>Once</c> no latchea con Failed.
    /// </para>
    /// <para>
    /// <b>Sólo animación, sin impacto.</b> El arqueo es el anuncio de la Fase 2 y pasa una única vez
    /// en la pelea: sin un gesto que lo ocupe, la mitad más importante del kit del jefe entra como
    /// un número de curación que aparece solo. Pero no lleva VFX ni Feel de impacto — el jugador
    /// pierde oro, no vida, y el chispazo de golpe le haría leer un daño que no existe.
    /// </para>
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
        /// Event key del Animation Event del clip del jefe. Ver
        /// <c>AINode_CashierRangedShot.ImpactEventKey</c>: no es campo autorable para que un
        /// <c>ED_Boss_*</c> ya serializado no lo deserialice vacío.
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

            // Red de seguridad: el arqueo es el pasaje a Fase 2 y corre una sola vez — si se perdiera
            // por falta de presentación, el jefe se quedaría en Fase 1 el resto de la pelea.
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

            int heal = Mathf.Min(collected, MaxHeal);
            if (heal > 0) ApplyHeal(attrs, context, heal);
        }

        /// <remarks>
        /// Un solo step: el arqueo no golpea a nadie, así que no hay impacto que anclar sobre el
        /// jugador. El request se arma a mano porque el nodo no nace de un effect pass y no tiene
        /// <c>EffectContext</c> que pasarle — mismo caso que <c>CombatDeathWatcher</c>.
        /// </remarks>
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

            // SelfMaxHp es el cap del spawn (misma fuente que PcOwnerHpBelow). Sin baseline no se
            // clampea: preferimos curar de más que perder la curación del arqueo entero.
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
