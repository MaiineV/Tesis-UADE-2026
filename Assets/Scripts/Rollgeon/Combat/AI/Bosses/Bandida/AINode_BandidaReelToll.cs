using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Rolls;
using Rollgeon.Feedback;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// El peaje de la fila: cada turno del jefe, la máquina le drena rolls del pool al jugador —
    /// uno por cada rodillo vivo que todavía se pueda romper, hasta <see cref="Cap"/>.
    /// </summary>
    /// <remarks>
    /// El rodillo trabado por <c>AINode_LockReel</c> queda fuera del conteo: es inrompible, así que
    /// cobrar por él sería cobrar por algo que el jugador no puede contestar. <b><see cref="Cap"/>
    /// debe quedar muy por debajo del grant por turno</b> (<c>RollsPerTurn</c> = 5), o el jugador
    /// entra en economía neta negativa para siempre. Siempre <see cref="AIResult.Succeeded"/>: un
    /// <c>Failed</c> acá le cortaría al jefe el resto del turno.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_BandidaReelToll : AIActionNode
    {
        [Tooltip("Máximo de rolls a drenar por turno. Fase 1 = 1, Fase 2 = 2. Mantener muy por " +
                 "debajo del grant por turno (5) para no dejar al jugador en economía negativa.")]
        [MinValue(0)]
        public int Cap = 1;

        public override string NodeName => $"Reel Toll (≤{Cap} rolls)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.PlayerGuid == Guid.Empty) return AIResult.Succeeded;

            int owed = ResolveOwed();
            if (owed <= 0) return AIResult.Succeeded;

            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null)
            {
                Debug.LogError("[AINode_BandidaReelToll] IRollPoolService no registrado — la fila no " +
                               "cobra peaje y la presión de romper rodillos desaparece.");
                return AIResult.Succeeded;
            }

            int drained = rolls.Drain(context.PlayerGuid, owed);
            if (drained > 0) Announce(context, drained);

            return AIResult.Succeeded;
        }

        /// <summary>Rodillos vivos y rompibles, capados. 0 si el servicio no está.</summary>
        private int ResolveOwed()
        {
            if (!ServiceLocator.TryGetService<IBandidaJackpotService>(out var jackpot) || jackpot == null)
                return 0;

            var slots = jackpot.Slots;
            if (slots == null) return 0;

            int breakable = 0;
            foreach (var slot in slots)
            {
                if (slot != null && slot.IsAlive && !slot.Locked) breakable++;
            }

            return breakable < Cap ? breakable : Cap;
        }

        /// <summary>Número flotante sobre el jugador + el manotazo de la máquina.</summary>
        /// <remarks>
        /// <see cref="FloatingNumberType.Status"/> y no <c>Damage</c>: el jugador pierde rolls, no
        /// vida. El drain parcial lo maneja <see cref="IRollPoolService.Drain"/> (floor en 0, devuelve
        /// lo efectivamente cobrado). No bloquea el turno (sin <c>BeginFeedbackWait</c>) porque es un
        /// cobro pasivo que pasa todos los turnos.
        /// </remarks>
        private static void Announce(AIContext context, int drained)
        {
            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                context.PlayerGuid,
                FloatingNumberType.Status,
                (float)drained,
                Vector3.zero);

            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) return;

            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep>
                {
                    Step(BossFeedbackIds.BandidaArmAnim),
                },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, null);
        }

        private static FeedbackSequenceStep Step(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.Immediate,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };
    }
}
