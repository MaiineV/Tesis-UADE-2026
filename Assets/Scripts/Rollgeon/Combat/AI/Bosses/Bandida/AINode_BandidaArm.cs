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

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// El brazo de La Bandida: 12 de daño melee directo a quien haya terminado su turno pegado a la
    /// máquina. Sin marca y sin área. Ficha de diseño "La Bandida" (piso 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué dejó de ser un <c>TelegraphMark</c>.</b> Como marca de 3×3 sobre el jefe, el brazo
    /// avisaba un turno antes y se esquivaba con un paso: era daño que nunca entraba y una amenaza
    /// más compitiendo por lectura con el número del jackpot. Como golpe directo es el precio fijo de
    /// desarmar de cerca — los rodillos están en el anillo del jefe, así que romperlos es quedar a su
    /// alcance, y esa es la decisión que la pelea quiere cobrar.
    /// </para>
    /// <para>
    /// <b>"Termina el turno pegado" se resuelve mirando el presente.</b> El jefe actúa después del
    /// jugador (CNF-006), así que la posición que se lee en su turno ES la posición en la que el
    /// jugador cerró el suyo: no hace falta recordar nada. Mismo patrón que
    /// <c>AINode_TahurPoke</c>.
    /// </para>
    /// <para>
    /// <b>Se auto-gatea.</b> El árbol ya lo envuelve en un <c>If(PcTargetInRange)</c>, pero el nodo
    /// vuelve a medir la distancia: un rewire que se olvide la condición no puede convertir el brazo
    /// en un golpe a distancia, que es justo lo que el jefe atornillado a la pared no puede tener.
    /// El <see cref="Metric"/> tiene que ser el mismo que el del gate o una de las dos mitades
    /// miente sobre las diagonales.
    /// </para>
    /// <para>
    /// <b>Sin <c>FaceTarget</c>, a diferencia de <c>AINode_CashierRangedShot</c>.</b> Los dos jefes
    /// que giran hacia el jugador antes de pegar se mueven por la sala y pueden quedar de espaldas;
    /// esta máquina está atornillada y su ataque es una palanca que baja, no un puño que apunta.
    /// Girarla sería mueble rotando contra la pared.
    /// </para>
    /// <para>
    /// <b>La presentación es media mecánica.</b> El brazo es el único cobro del jefe que no avisa: sin
    /// palanca bajando ni impacto, el jugador ve aparecer un 12 y no tiene con qué atar ese 12 a
    /// "estaba pegado a la máquina". La secuencia se corre en el camino coroutine y <b>retiene el
    /// turno</b> hasta terminar, igual que <c>AINode_ExecuteTelegraph</c>: soltar antes dejaría al
    /// jefe encadenando el resto del sequence con el brazo todavía en el aire.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_BandidaArm : AIActionNode
    {
        [Tooltip("Daño del brazo. Directo, sin marca previa.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas. 1 = pegado a la máquina.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador. Chebyshev incluye las diagonales — tiene que " +
                 "coincidir con la del PcTargetInRange que gatea el nodo.")]
        public DistanceMetric Metric = DistanceMetric.Chebyshev;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Animación de la palanca bajando. Vacío = el id canónico del brazo (ver remarks).")]
        public string ArmFeedbackId = BossFeedbackIds.BandidaArmAnim;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("VFX de impacto sobre el jugador, al terminar el golpe. Vacío = el id canónico.")]
        public string ImpactVfxId = BossFeedbackIds.BandidaImpactVfx;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feel (hitstop/shake) del impacto. Vacío = el id canónico.")]
        public string ImpactFeelId = BossFeedbackIds.BandidaImpactFeel;

        [Tooltip("Event key del Animation Event que marca el frame del golpe. Hoy ningún clip del rig " +
                 "Mecha publica eventos, así que vacío es lo correcto: el daño cae al terminar la " +
                 "secuencia. Cuando el clip tenga su evento, ponerlo acá adelanta el número al golpe.")]
        public string ImpactEventKey;

        public override string NodeName => $"Bandida — Arm ({Damage})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): el daño y nada más. No hay
        /// dónde esperar una animación, y bloquear acá colgaría los tests.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!CanStrike(context)) return AIResult.Failed;

            Strike(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: palanca → impacto sobre el jugador, con el daño aterrizando en el
        /// golpe si el clip publica <see cref="ImpactEventKey"/> y al final de la secuencia si no.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!CanStrike(context))
            {
                onResult?.Invoke(AIResult.Failed);
                yield break;
            }

            bool resolved = false;
            Action strikeOnce = () =>
            {
                if (resolved) return;
                resolved = true;
                Strike(context);
            };

            var swing = PlaySwing(context, strikeOnce);
            while (swing.MoveNext()) yield return swing.Current;

            // Red de seguridad: sin feedback service, con un id huérfano o con el watchdog cortando
            // la secuencia, el 12 igual se cobra. La presentación nunca es dueña del gameplay.
            strikeOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        /// <summary>
        /// El gate completo del nodo. Se separa del golpe porque el camino coroutine tiene que
        /// decidir <b>antes</b> de la animación: una palanca que baja sobre nadie es peor que no
        /// animar, y además retendría el turno para nada.
        /// </summary>
        private bool CanStrike(AIContext context)
        {
            if (context?.Grid == null) return false;
            if (context.DamagePipeline == null || Damage <= 0) return false;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return false;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            return distance <= Mathf.Max(1, Range);
        }

        private void Strike(AIContext context)
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
        /// Request de secuencia armado a mano en vez de un <c>EffPlaySequence</c>: el nodo no nace de
        /// un effect pass, así que no tiene <c>EffectContext</c> que pasarle (mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null).
        /// </remarks>
        private IEnumerator PlaySwing(AIContext context, Action onImpact)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // Impacto encadenado al final de la palanca (AfterStep 0) y no gateado por evento: ningún
            // clip del rig Mecha publica keys, así que un StartMode=OnEvent dejaría los dos steps
            // esperando algo que no llega hasta que el watchdog mate la secuencia.
            var steps = new List<FeedbackSequenceStep>(3)
            {
                new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = Authored(ArmFeedbackId, BossFeedbackIds.BandidaArmAnim),
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                },
                Impact(Authored(ImpactVfxId, BossFeedbackIds.BandidaImpactVfx)),
                Impact(Authored(ImpactFeelId, BossFeedbackIds.BandidaImpactFeel)),
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

            bool impactFired = string.IsNullOrEmpty(ImpactEventKey);

            // Se envuelve el wait canónico (trae su propio timeout + force-reset del depth) en vez de
            // rehacer el loop: el bus es latched, así que pollear HasFired por frame alcanza para
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

        /// <summary>VFX/Feel del impacto: arrancan juntos cuando la palanca terminó de bajar.</summary>
        private static FeedbackSequenceStep Impact(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.AfterStep,
            StartDependsOnStepIndex = 0,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

        /// <summary>
        /// Campo vacío ⇒ el id canónico del jefe. No es una comodidad: Odin instancia el nodo sin
        /// correr field initializers y los <c>ED_Boss_Bandida</c> ya autorados no traen estos campos,
        /// así que un default por inicializador nunca llegaría al asset y el brazo seguiría mudo.
        /// Silenciar un canal se hace apuntándolo a otra entry, no vaciándolo — un ataque de jefe sin
        /// momento en pantalla es exactamente el bug que este nodo arregla.
        /// </summary>
        private static string Authored(string authored, string canonical)
            => string.IsNullOrEmpty(authored) ? canonical : authored;

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
