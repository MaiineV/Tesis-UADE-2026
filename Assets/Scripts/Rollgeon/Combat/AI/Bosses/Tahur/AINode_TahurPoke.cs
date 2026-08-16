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
    /// El poke del Tahúr: 12 de daño melee, solo en ronda limpia. Es el precio fijo de cobrar,
    /// porque cobrar es estar en su cara. Ficha de diseño "El Tahúr" (piso 3).
    /// </summary>
    /// <remarks>
    /// <b>Exclusivo de la rama de marcar.</b> El poke y el Castigo nunca resuelven la misma ronda:
    /// 12 + 45 rompe el techo de 45 por golpe del piso 3. El árbol ya lo gatea con
    /// <c>PcTahurCleanRound</c>, pero el nodo se auto-gatea igual — un rewire que se olvide la
    /// condición no puede convertirse en un golpe de 57.
    /// </remarks>
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

        /// <summary>
        /// Key del Animation Event que marca el frame de impacto.
        /// </summary>
        /// <remarks>
        /// No es autorable: es un hecho del clip, no una decisión de diseño.
        /// <c>Anim_SunkedGrand_Attack_Melee</c> publica "hit" a los 0.467 s vía
        /// <c>AnimationFeedbackEvent</c>, así que los 12 cobran cuando el pinche entra y no cuando
        /// el clip termina. Si el rig cambiara a uno sin eventos, el latch de abajo lo cubre.
        /// </remarks>
        private const string ImpactEventKey = "hit";

        public override string NodeName => $"Tahúr — Poke ({Damage})";

        /// <remarks>
        /// Vacío significa "el id canónico del nodo", no "sin animación": Odin puede deserializar
        /// un <c>ED_Boss_*.asset</c> viejo sin correr los field initializers, así que un default en
        /// el campo llegaría en null y el pinche volvería a entrar invisible.
        /// </remarks>
        private string AnimFeedbackId => string.IsNullOrEmpty(AnimFeedbackIdOverride)
            ? BossFeedbackIds.TahurPokeAnim
            : AnimFeedbackIdOverride;

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): cobra sin animación,
        /// porque no hay dónde esperarla.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanPoke(context)) return AIResult.Failed;
            Poke(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: aterriza el daño en el frame del pinche y <b>retiene el turno hasta
        /// que el clip termina</b>. Soltar en el impacto dejaba al jefe poniendo la mesa con medio
        /// ataque todavía reproduciéndose.
        /// </summary>
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

            // Red de seguridad: si el clip perdiera su Animation Event, o faltara el
            // FeedbackService o el TurnManager, los 12 igual caen — tarde, pero caen.
            pokeOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

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

        /// <remarks>
        /// El request se arma a mano en vez de reusar <c>EffPlaySequence</c>: el nodo no nace de un
        /// effect pass, así que no tiene <c>EffectContext</c> que pasarle (mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null).
        /// </remarks>
        private IEnumerator PlayPoke(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // Los tres steps arrancan juntos, como la secuencia de golpe autorada a mano de
            // ED_MeleeCardEnemy. El impacto NO se cuelga de StartMode.OnEvent: un step esperando
            // una key que el clip no publique queda girando para siempre, mientras que el daño sí
            // puede engancharse al evento porque su latch tiene salida por tiempo.
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

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el daño no queda
            // sincronizado. Mismo degradado que EffPlaySequence.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool impactFired = false;

            // Se envuelve el wait canónico (trae su propio timeout + force-reset del depth) en vez
            // de rehacer el loop: el bus es latched, así que pollear HasFired por frame alcanza para
            // enganchar el Animation Event sin suscribirse a nada.
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

        /// <summary>
        /// Gira al Tahúr hacia el jugador antes del pinche. Sin esto pincha de espaldas cuando el
        /// jugador se le metió en la mesa por detrás.
        /// </summary>
        private static void FaceTarget(AIContext context)
        {
            if (!ServiceLocator.TryGetService<Entities.Visuals.IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var from)) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var to)) return;
            pawn.FaceCoord(from, to);
        }

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
