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
    /// El cubilete de La Generala: cuando tira, baja la copa sobre quien esté pegado a ella y le
    /// cobra <see cref="Damage"/> directos. Ficha de diseño "La Generala" (piso 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el precio de romper de cerca.</b> Los cinco dados son la mano del jefe y romperlos es
    /// la jugada que le borra categorías, pero romperlos es acercarse. Sin este golpe la mesa se
    /// desarma gratis: el resto de su daño viaja por telegraphs avisados una ronda antes, o sea
    /// esquivables sin renunciar a nada.
    /// </para>
    /// <para>
    /// <b>Directo, no avisado.</b> No marca área ni pinta overlay — el aviso es la distancia, que el
    /// jugador controla entero. Por eso el nodo tiene que ir envuelto en un <c>Selector[nodo, Wait]</c>:
    /// con el jugador lejos devuelve <see cref="AIResult.Failed"/> y sin la envoltura le cancelaría
    /// al jefe el resto del turno, el telegraph de la mano incluido.
    /// </para>
    /// <para>
    /// <b>Manhattan por default.</b> Es el mismo alcance con el que el jugador la ataca a ella
    /// (<c>Base Attack</c>: Range 1, RangeMode Manhattan), así que la regla se lee de una: si podés
    /// pegarle, te alcanza. Con <see cref="DistanceMetric.Chebyshev"/> el cubilete recupera el 3×3
    /// completo — incluidas las diagonales, desde donde el jugador no puede atacar.
    /// </para>
    /// </remarks>
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

        /// <remarks>
        /// Vacío significa "el id canónico del nodo", no "sin animación": Odin puede deserializar
        /// un <c>ED_Boss_*.asset</c> viejo sin correr los field initializers, así que un default en
        /// el campo llegaría en null y el cubilete volvería a caer invisible.
        /// </remarks>
        private string AnimFeedbackId => string.IsNullOrEmpty(AnimFeedbackIdOverride)
            ? BossFeedbackIds.GeneralaCupSlamAnim
            : AnimFeedbackIdOverride;

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): cobra sin animación,
        /// porque no hay dónde esperarla.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanSlam(context)) return AIResult.Failed;
            Slam(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: baja la copa y <b>retiene el turno hasta que el clip termina</b>.
        /// Sin la retención el siguiente hijo del sequence (el telegraph de la mano) marcaba encima
        /// del cubilete todavía en el aire y los dos gestos se pisaban.
        /// </summary>
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

            // Red de seguridad: sin FeedbackService, sin TurnManager o con la entry mal autorada
            // el golpe igual cae — tarde, pero cae. El daño nunca depende de que se vea.
            slamOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

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

        /// <remarks>
        /// El request se arma a mano en vez de reusar <c>EffPlaySequence</c>: el nodo no nace de un
        /// effect pass, así que no tiene <c>EffectContext</c> que pasarle (mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null).
        /// </remarks>
        private IEnumerator PlaySlam(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // Los tres steps arrancan juntos, como la secuencia de golpe autorada a mano de
            // ED_MeleeCardEnemy: un StartMode.OnEvent que el clip nunca publica deja el step
            // girando para siempre, y los clips de DiceBoss_Animated tienen m_Events vacío.
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

            // Sin Animation Event al que atarse, el daño cae con el VFX de impacto en vez de al
            // final del clip: diferirlo dejaba el número flotante casi un segundo detrás de la copa.
            onImpact?.Invoke();

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el turno no se
            // retiene. Mismo degradado que EffPlaySequence.
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

        /// <summary>
        /// Gira a la Generala hacia el jugador antes de bajar la copa. Sin esto queda mirando en la
        /// dirección en la que kiteó el turno anterior y golpea de espaldas.
        /// </summary>
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
        // Dropdown obligatorio (§0): los ids de feedback nunca se tipean a mano.
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
