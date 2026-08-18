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
    /// Va primero en el Sequence raíz y siempre devuelve <see cref="AIResult.Succeeded"/>: "no había
    /// nada marcado" y "el jugador se fue del sector" son resoluciones válidas. Un golpe por sector y
    /// no uno sumado, para que cada golpe quede bajo el techo de daño del piso.
    /// </para>
    /// <para>
    /// Campo de id vacío ⇒ el id canónico (<see cref="BossFeedbackIds"/>): Odin no corre field
    /// initializers, así que un <c>ED_Boss_Croupier</c> ya autorado no trae estos campos. Para
    /// silenciar un canal se lo apunta a otra entry, no se lo vacía.
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
        /// presentación — bloquear acá colgaría los tests.
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
        /// Request de secuencia a mano y no <c>EffPlaySequence</c>: el nodo no nace de un effect pass
        /// y no tiene <c>EffectContext</c> que pasarle (por eso <c>FeedbackRequest.Context</c> admite null).
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

            // Sin TurnManager no hay gate que esperar: el impacto corre igual, pero el turno del jefe
            // le pasa por encima.
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
