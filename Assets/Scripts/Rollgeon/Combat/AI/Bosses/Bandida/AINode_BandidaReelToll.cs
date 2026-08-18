using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Feedback;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// El peaje de la fila: cada turno del jefe, la máquina le cobra energía al jugador — una por
    /// cada rodillo vivo que todavía se pueda romper, hasta <see cref="Cap"/>.
    /// </summary>
    /// <remarks>
    /// El rodillo trabado por <c>AINode_LockReel</c> queda fuera del conteo: es inrompible, así que
    /// cobrar por él sería cobrar por algo que el jugador no puede contestar. <b><see cref="Cap"/> no
    /// puede pasar del regen del jugador</b> (<c>EnergyRegenBase</c> = 2), o queda en energía neta
    /// negativa para siempre. Siempre <see cref="AIResult.Succeeded"/>: un <c>Failed</c> acá le
    /// cortaría al jefe el resto del turno.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_BandidaReelToll : AIActionNode
    {
        [Tooltip("Máximo de energía a drenar por turno. Fase 1 = 1, Fase 2 = 2. Con el regen del " +
                 "jugador en 2, un techo mayor lo deja en energía neta negativa para siempre.")]
        [MinValue(0)]
        public int Cap = 1;

        public override string NodeName => $"Reel Toll (≤{Cap} energía)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.PlayerGuid == Guid.Empty) return AIResult.Succeeded;

            int owed = ResolveOwed();
            if (owed <= 0) return AIResult.Succeeded;

            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null)
            {
                Debug.LogError("[AINode_BandidaReelToll] IEnergyService no registrado — la fila no " +
                               "cobra peaje y la presión de romper rodillos desaparece.");
                return AIResult.Succeeded;
            }

            int drained = Drain(energy, context.PlayerGuid, owed);
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

        /// <summary>Cobra de a uno hasta <paramref name="amount"/> o hasta que el jugador quede seco.</summary>
        /// <remarks>
        /// <c>SpendEnergy</c> es todo-o-nada —<c>false</c> sin mutar si <c>cost &gt; current</c>— así
        /// que pedir los dos de una dejaría al jugador con 1 de energía pagando cero. Y es el path
        /// canónico: mutar el atributo a mano se saltea el payload que el HUD necesita para repintarse.
        /// </remarks>
        private static int Drain(IEnergyService energy, Guid playerGuid, int amount)
        {
            int drained = 0;
            for (int i = 0; i < amount; i++)
            {
                if (!energy.SpendEnergy(playerGuid, 1)) break;
                drained++;
            }
            return drained;
        }

        /// <summary>Número flotante sobre el jugador + el manotazo de la máquina.</summary>
        /// <remarks>
        /// <see cref="FloatingNumberType.Status"/> y no <c>Damage</c>: el jugador pierde energía, no
        /// vida. No bloquea el turno (sin <c>BeginFeedbackWait</c>) porque es un cobro pasivo que pasa
        /// todos los turnos.
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
