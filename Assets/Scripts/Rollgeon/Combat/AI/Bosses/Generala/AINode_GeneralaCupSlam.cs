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

namespace Rollgeon.Combat.AI.Bosses.Generala
{
    /// <summary>
    /// Con el jugador lejos devuelve <see cref="AIResult.Failed"/>, así que tiene que ir envuelto en
    /// <c>Selector[nodo, Wait]</c>: suelto le cancelaría al jefe el resto del turno, el telegraph de
    /// la mano incluido.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_GeneralaCupSlam : AIActionNode
    {
        [Tooltip("Daño directo del cubilete. No se avisa: cobra en el acto, el mismo turno en que tira.")]
        [MinValue(0)]
        public int Damage = 18;

        [Tooltip("Alcance en casillas. 1 = pegado.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador. Manhattan = las cuatro casillas desde las que el " +
                 "jugador puede atacarla. Chebyshev = el 3×3 entero, diagonales incluidas.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Override de la animación del cubilete. Vacío = " + BossFeedbackIds.GeneralaCupSlamAnim +
                 ". Existe porque en DiceBoss_Animated el cubilete también se puede leer con 'Roll' " +
                 "(" + BossFeedbackIds.GeneralaRollAnim + "), que es literalmente el gesto de tirar.")]
        public string AnimFeedbackIdOverride;

        public override string NodeName => $"Generala — Cubilete ({Damage} melee)";

        /// <summary>Vacío significa "el id canónico", no "sin animación": Odin no corre los field initializers al deserializar un <c>ED_Boss_*.asset</c>.</summary>
        private string AnimFeedbackId => string.IsNullOrEmpty(AnimFeedbackIdOverride)
            ? BossFeedbackIds.GeneralaCupSlamAnim
            : AnimFeedbackIdOverride;

        /// <summary>Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): cobra sin animación, porque no hay dónde esperarla.</summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanSlam(context)) return AIResult.Failed;
            Slam(context);
            return AIResult.Succeeded;
        }

        /// <summary><b>Retiene el turno hasta que el clip termina</b> — sin eso el telegraph de la mano marca encima del cubilete todavía en el aire.</summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!CanSlam(context))
            {
                onResult?.Invoke(AIResult.Failed);
                yield break;
            }

            FaceTarget(context);

            bool resolved = false;
            Action slamOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Slam(context);
            };

            var beat = PlaySlam(context, slamOnce);
            while (beat.MoveNext()) yield return beat.Current;

            // Red de seguridad: sin presentación el golpe igual cae — tarde, pero cae.
            slamOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        private bool CanSlam(AIContext context)
        {
            if (context?.Grid == null) return false;
            if (context.SelfGuid == Guid.Empty || context.PlayerGuid == Guid.Empty) return false;
            if (context.DamagePipeline == null || Damage <= 0) return false;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;

            return Distance(selfCoord, playerCoord) <= Mathf.Max(1, Range);
        }

        private void Slam(AIContext context)
        {
            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = Damage,
                Kind = Kind,
            });
        }

        private IEnumerator PlaySlam(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // Los tres steps arrancan juntos: un StartMode.OnEvent que el clip nunca publica deja el
            // step girando para siempre, y los clips de DiceBoss_Animated tienen m_Events vacío.
            var steps = new List<FeedbackSequenceStep>
            {
                Step(AnimFeedbackId),
                Step(BossFeedbackIds.GeneralaImpactVfx),
                Step(BossFeedbackIds.GeneralaImpactFeel),
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

            // Sin Animation Event al que atarse, el daño cae con el VFX: diferirlo al final del clip
            // deja el número flotante casi un segundo detrás de la copa.
            onImpact?.Invoke();

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        private static FeedbackSequenceStep Step(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.Immediate,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

        /// <summary>Sin esto queda mirando en la dirección en la que kiteó el turno anterior y golpea de espaldas.</summary>
        private static void FaceTarget(AIContext context)
        {
            if (!ServiceLocator.TryGetService<Entities.Visuals.IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var from)) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var to)) return;
            pawn.FaceCoord(from, to);
        }

        private int Distance(GridCoord from, GridCoord to) => Metric == DistanceMetric.Manhattan
            ? from.Manhattan(to)
            : from.Chebyshev(to);

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
