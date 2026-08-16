using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Detona los sectores que el Croupier cantó el turno pasado: consume el área pendiente de cada
    /// slot y, si el jugador está adentro, aplica su daño. Cierra el windup — a partir de acá pegarle
    /// al jefe ya no corre la rueda hasta que vuelva a cantar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va primero en el Sequence raíz, igual que <c>AINode_ExecuteTelegraph</c>, y como él devuelve
    /// siempre <see cref="AIResult.Succeeded"/>: "no había nada marcado" (turno 1) o "el jugador se
    /// fue del sector" son resoluciones válidas, no fallos que deban cortarle el turno al jefe.
    /// </para>
    /// <para>
    /// <b>Un golpe por sector, no un golpe por casilla.</b> En fase 2 las dos áreas se resuelven una
    /// por una, así que el jugador parado en la columna de costura recibe dos impactos de 12 (24 en el
    /// turno) en vez de uno de 24. Dos hits mantienen cada golpe individual bajo el techo de daño del
    /// piso y hacen que escudo/mitigación se apliquen como en cualquier otro par de golpes.
    /// </para>
    /// <para>
    /// <b>El impacto se presenta, la explosión no.</b> El sector que cae es el daño grande del jefe y
    /// hasta ahora se resolvía en silencio: los tiles se apagaban y aparecía un 20. El VFX + Feel van
    /// sobre el <i>jugador</i> y sólo si el golpe entró — el paño detonando ya tiene su propia lectura
    /// en el overlay, y celebrar un sector vacío le enseñaría al jugador a ignorar el efecto. Sin
    /// animación de jefe: acá no hay gesto del Croupier, explota el paño.
    /// </para>
    /// <para>
    /// <b>Campo de id vacío ⇒ el id canónico del jefe</b> (<see cref="BossFeedbackIds"/>). No es una
    /// comodidad: Odin instancia el nodo sin correr field initializers y los <c>ED_Boss_Croupier</c>
    /// ya autorados no traen estos campos, así que un default por inicializador nunca llegaría al
    /// asset y el impacto seguiría invisible. Para silenciar un canal se lo apunta a otra entry, no
    /// se lo vacía.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_DetonateSungSectors : AIActionNode
    {
        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("VFX del impacto sobre el jugador alcanzado. Vacío = el id canónico (ver remarks).")]
        public string ImpactVfxId = BossFeedbackIds.CroupierImpactVfx;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feel (hitstop/shake) del impacto. Vacío = el id canónico.")]
        public string ImpactFeelId = BossFeedbackIds.CroupierImpactFeel;

        public override string NodeName => "Detonate Sung Sectors (Croupier)";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): la resolución completa sin
        /// presentación. No hay dónde esperar el impacto, y bloquear acá colgaría los tests.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            Detonate(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode. El daño cae primero y el impacto se reproduce después, en el mismo
        /// frame: la presentación nunca puede quedar entre el jugador y el golpe que ya se decidió.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (Detonate(context))
            {
                var impact = PlayImpact(context);
                while (impact.MoveNext()) yield return impact.Current;
            }

            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        /// <returns><c>true</c> si el golpe alcanzó al jugador — lo único que amerita impacto.</returns>
        private static bool Detonate(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return false;

            var wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return false;

            // Se cierra el windup ANTES de resolver el daño: el golpe que detona puede matar al
            // jugador y disparar el fin del combate, y con el windup abierto la rueda quedaría
            // esperando un corrimiento de una pelea que ya terminó.
            var slots = wheel.ConsumeWindup();
            if (slots == null || slots.Count == 0) return false;

            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat);

            bool anyHit = false;
            foreach (var slot in slots)
            {
                var slotGuid = CroupierSectorTelegraph.SlotGuid(context.SelfGuid, slot.Slot);
                CroupierSectorTelegraph.ClearOverlay(context.SelfGuid, slot.Slot);

                if (threat == null || !threat.TryConsume(slotGuid, out var area)) continue;
                if (Resolve(context, area)) anyHit = true;
            }

            EventManager.Trigger(EventName.OnThreatenedAreaResolved, context.SelfGuid, anyHit);
            return anyHit;
        }

        /// <remarks>
        /// Request de secuencia armado a mano en vez de un <c>EffPlaySequence</c>: el nodo no nace de
        /// un effect pass, así que no tiene <c>EffectContext</c> que pasarle (mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null).
        /// </remarks>
        private IEnumerator PlayImpact(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            var steps = new List<FeedbackSequenceStep>(2)
            {
                Impact(string.IsNullOrEmpty(ImpactVfxId) ? BossFeedbackIds.CroupierImpactVfx : ImpactVfxId),
                Impact(string.IsNullOrEmpty(ImpactFeelId) ? BossFeedbackIds.CroupierImpactFeel : ImpactFeelId),
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

            // Sin TurnManager no hay gate que esperar — el impacto igual corre, pero el turno del jefe
            // le pasa por encima. Mismo degradado que EffPlaySequence.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        /// <summary>VFX y Feel arrancan juntos: son las dos mitades del mismo instante.</summary>
        private static FeedbackSequenceStep Impact(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.Immediate,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

        private static bool Resolve(AIContext context, ThreatenedArea area)
        {
            var grid = context.Grid;
            if (grid == null) return false;
            if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return false;
            if (!area.Contains(playerCoord)) return false;

            if (context.DamagePipeline != null && area.Damage > 0)
            {
                context.DamagePipeline.Resolve(new DamageContext
                {
                    // El source es el jefe, no el guid derivado del slot: la atribución del daño, la
                    // debilidad y el feedback siguen apuntando al Croupier.
                    SourceId = context.SelfGuid,
                    TargetId = context.PlayerGuid,
                    BaseDamage = area.Damage,
                    Kind = area.Kind,
                });
            }
            return true;
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
