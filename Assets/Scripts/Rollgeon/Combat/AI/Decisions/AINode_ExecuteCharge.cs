using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Cobra la carga marcada por <see cref="AINode_TelegraphMark"/> (turno N+1), pero a
    /// diferencia de <see cref="AINode_ExecuteTelegraph"/> no es una zona de daño estática: el
    /// dueño se REUBICA junto al jugador (toma la casilla que el jugador tenía, del lado por el
    /// que venía cargando) y empuja al jugador 1 casilla más allá. Si esa casilla de empuje está
    /// bloqueada (pared, límite de sala, otro ocupante), no empuja y suma +<see
    /// cref="BlockedDamageBonus"/>×daño en su lugar — mismo criterio que <c>AttackMeleeWithPush</c>,
    /// aplicado acá porque la carga es del dueño (necesita reubicarse), no un Eff sobre el target.
    /// </summary>
    /// <remarks>
    /// Duplica el esqueleto de windup/feedback de <see cref="AINode_ExecuteTelegraph"/> (no hay
    /// base común entre los dos — mismo criterio que otros duplicados puntuales del proyecto)
    /// porque el Resolve es distinto: mueve al dueño y al jugador en vez de solo aplicar daño.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ExecuteCharge : AIActionNode, IAIIntentNode
    {
        [Title("Windup")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feedback que corre al cobrar la carga, antes de resolver. Vacío = sin animación.")]
        public string WindupFeedbackId;

        [ShowIf(nameof(HasWindup))]
        public string ImpactEventKey = "hit";

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Segundo feedback, opcional, que corre DESPUÉS de que termine el windup (ej. un " +
                 "recule tras el impacto). Vacío = solo el windup, un paso. No espera ImpactEventKey.")]
        public string FollowUpFeedbackId;

        [Tooltip("Bono de daño (fracción del daño base) si el empuje queda bloqueado y no se puede aplicar.")]
        [Min(0f)]
        public float BlockedDamageBonus = 0.5f;

        public override string NodeName => "Execute Charge (turn N+1)";

        public override AIResult Tick(AIContext context)
        {
            if (!TryConsumePending(context, out var area)) return AIResult.Succeeded;
            Resolve(context, area);
            return AIResult.Succeeded;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!TryConsumePending(context, out var area))
            {
                onResult?.Invoke(AIResult.Succeeded);
                yield break;
            }

            FaceTarget(context);

            bool resolved = false;
            Action resolveOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Resolve(context, area);
            };

            var windup = PlayWindup(context, resolveOnce);
            while (windup.MoveNext()) yield return windup.Current;

            resolveOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        public bool TryDescribeIntent(AIContext context, out AIIntent intent)
        {
            intent = default;
            if (context == null) return false;
            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
                return false;
            if (!threat.TryPeek(context.SelfGuid, out var area)) return false;

            intent = new AIIntent(AIIntentTextKeys.Telegraph, "Carga marcada", area.Damage, area.Kind, area.Tiles);
            return true;
        }

        public bool TryDescribeOption(AIContext context, out AIIntent intent)
        {
            intent = default;
            return false;
        }

        private static bool TryConsumePending(AIContext context, out ThreatenedArea area)
        {
            area = default;
            if (context == null) return false;
            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                ClearOverlay(context);
                return false;
            }
            if (threat.TryConsume(context.SelfGuid, out area)) return true;
            ClearOverlay(context);
            return false;
        }

        private static void ClearOverlay(AIContext context)
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(context.SelfGuid);
        }

        private void Resolve(AIContext context, ThreatenedArea area)
        {
            ClearOverlay(context);

            var grid = context.Grid;
            if (grid == null) return;
            if (!grid.OccupiesAny(context.PlayerGuid, area.Contains))
            {
                // El jugador se salió de la línea antes de que se cobrara — whiff, nadie se mueve.
                EventManager.Trigger(EventName.OnThreatenedAreaResolved, context.SelfGuid, false);
                return;
            }

            if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)
                || !grid.TryGetPosition(context.PlayerGuid, out var playerCoord))
                return;

            var dir = CardinalExtensions.FromDelta(selfCoord, playerCoord);
            var opposite = dir.Clockwise().Clockwise();

            var pushTarget = dir.Step(playerCoord);
            bool pushFree = grid.IsWalkable(pushTarget) && !grid.IsOccupied(pushTarget);

            int damage = pushFree ? area.Damage : Mathf.RoundToInt(area.Damage * (1f + Mathf.Max(0f, BlockedDamageBonus)));

            if (pushFree) context.Movement?.Move(context.PlayerGuid, pushTarget);

            // El empuje ya movió al jugador (si pudo), así que su casilla vieja queda libre y el
            // charger la ocupa directo — eso lo deja adyacente al jugador empujado. Si el empuje
            // se bloqueó, el jugador sigue ahí parado: el charger se frena una casilla antes.
            var selfDestination = pushFree ? playerCoord : opposite.Step(playerCoord);
            if (grid.IsWalkable(selfDestination) && !grid.IsOccupied(selfDestination))
                context.Movement?.Move(context.SelfGuid, selfDestination);

            bool hit = true;
            if (context.DamagePipeline != null && damage > 0)
            {
                context.DamagePipeline.Resolve(new DamageContext
                {
                    SourceId = context.SelfGuid,
                    TargetId = context.PlayerGuid,
                    BaseDamage = damage,
                    Kind = area.Kind,
                });
            }

            EventManager.Trigger(EventName.OnThreatenedAreaResolved, context.SelfGuid, hit);
        }

        private static void FaceTarget(AIContext context)
        {
            if (context?.Grid == null || context.PlayerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<Entities.Visuals.IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var from)) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var to)) return;
            pawn.FaceCoord(from, to);
        }

        private IEnumerator PlayWindup(AIContext context, Action onImpact)
        {
            if (!HasWindup()) yield break;
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            var steps = new List<FeedbackSequenceStep>
            {
                new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = WindupFeedbackId,
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                },
            };
            // El paso 2 (ej. recule tras el impacto) recién arranca cuando el 1 termina — la
            // propia secuencia de feedback los encadena por Duration, sin timing a mano.
            if (!string.IsNullOrEmpty(FollowUpFeedbackId))
            {
                steps.Add(new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = FollowUpFeedbackId,
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                });
            }

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = steps,
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool impactFired = string.IsNullOrEmpty(ImpactEventKey);
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

        private bool HasWindup() => !string.IsNullOrEmpty(WindupFeedbackId);

#if UNITY_EDITOR
        private static IEnumerable<string> GetFeedbackIdsForDropdown()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FeedbackDBSO"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var db = UnityEditor.AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(path);
                if (db == null) continue;
                foreach (var id in db.GetAllFeedbackIds()) yield return id;
            }
        }
#endif
    }
}
