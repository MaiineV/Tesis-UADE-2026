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
    /// <summary>Campo de id vacío ⇒ el id canónico (<see cref="BossFeedbackIds"/>): Odin no corre field initializers y un asset ya autorado no trae estos campos.</summary>
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

        /// <summary>Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): bloquear acá colgaría los tests.</summary>
        public override AIResult Tick(AIContext context)
        {
            Detonate(context);
            return AIResult.Succeeded;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (Detonate(context))
            {
                var impact = PlayImpact(context);
                while (impact.MoveNext()) yield return impact.Current;
            }

            onResult?.Invoke(AIResult.Succeeded);
        }

        /// <returns><c>true</c> si el golpe alcanzó al jugador — lo único que amerita impacto.</returns>
        private static bool Detonate(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return false;

            var wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return false;

            // Se cierra el windup ANTES de resolver el daño: el golpe puede matar al jugador y
            // terminar el combate, y con el windup abierto la rueda queda esperando un corrimiento.
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

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

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
                    // El source es el jefe, no el guid derivado del slot: atribución, debilidad y
                    // feedback tienen que seguir apuntando al Croupier.
                    SourceId = context.SelfGuid,
                    TargetId = context.PlayerGuid,
                    BaseDamage = area.Damage,
                    Kind = area.Kind,
                });
            }
            return true;
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
