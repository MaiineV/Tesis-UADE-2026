using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// El poke y el Castigo nunca resuelven la misma ronda: sumados rompen el techo de daño por
    /// golpe del piso 3. El nodo se auto-gatea además del <c>PcTahurCleanRound</c> del árbol.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurPoke : AIActionNode
    {
        [Tooltip("Daño del poke.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas (Manhattan). 1 = pegado.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Tooltip("Exigir ronda limpia (que la liquidación de este turno no haya marcado Castigo). " +
                 "Apagarlo permite poke + Castigo en la misma ronda y rompe el techo de daño.")]
        public bool RequireCleanRound = true;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Override de la animación del pinche. Vacío = " + BossFeedbackIds.TahurPokeAnim + ".")]
        public string AnimFeedbackIdOverride;

        /// <summary>Es un hecho del clip, no un campo autorable.</summary>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Tahúr — Poke ({Damage})";

        /// <summary>Vacío significa "el id canónico", no "sin animación": Odin deserializa un <c>ED_Boss_*.asset</c> viejo sin correr los field initializers.</summary>
        private string AnimFeedbackId => string.IsNullOrEmpty(AnimFeedbackIdOverride)
            ? BossFeedbackIds.TahurPokeAnim
            : AnimFeedbackIdOverride;

        /// <summary>Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): cobra sin animación, porque no hay dónde esperarla.</summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanPoke(context)) return AIResult.Failed;
            Poke(context);
            return AIResult.Succeeded;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!CanPoke(context))
            {
                onResult?.Invoke(AIResult.Failed);
                yield break;
            }

            FaceTarget(context);

            bool resolved = false;
            Action pokeOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Poke(context);
            };

            var beat = PlayPoke(context, pokeOnce);
            while (beat.MoveNext()) yield return beat.Current;

            // Red de seguridad: sin presentación el poke igual cobra — tarde, pero cobra.
            pokeOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        private bool CanPoke(AIContext context)
        {
            if (context?.Grid == null) return false;

            if (RequireCleanRound)
            {
                var wager = TahurWagerService.ResolveOrCreate();
                if (wager.MarkedPunishmentThisTurn) return false;
            }

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            if (distance > Mathf.Max(1, Range)) return false;

            // LOS de proyecto: detrás de algo no hay bastonazo.
            if (!GridLineOfSight.HasClearLine(context.Grid, selfCoord, playerCoord,
                                              context.SelfGuid, context.PlayerGuid))
            {
                return false;
            }

            return context.DamagePipeline != null && Damage > 0;
        }

        private void Poke(AIContext context)
        {
            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = Damage,
                Kind = Kind,
            });
        }

        private IEnumerator PlayPoke(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // El impacto no se cuelga de StartMode.OnEvent: un step esperando una key que el clip no
            // publique gira para siempre.
            var steps = new List<FeedbackSequenceStep>
            {
                Step(AnimFeedbackId),
                Step(BossFeedbackIds.TahurImpactVfx),
                Step(BossFeedbackIds.TahurImpactFeel),
            };

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

            bool impactFired = false;

            // El bus es latched: pollear HasFired por frame engancha el Animation Event.
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

        private static FeedbackSequenceStep Step(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.Immediate,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

        private static void FaceTarget(AIContext context)
        {
            if (!ServiceLocator.TryGetService<Entities.Visuals.IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var from)) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var to)) return;
            pawn.FaceCoord(from, to);
        }

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
