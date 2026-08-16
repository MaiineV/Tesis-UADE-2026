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
    /// <para>
    /// <b>Qué agrega a la pelea.</b> La Bandida ya te pedía romper rodillos para cancelar el
    /// jackpot, pero esa presión era binaria y llegaba de golpe: mientras la cuenta no estuviera en
    /// cero, dejar la fila armada no costaba nada. El peaje convierte "todavía no rompí nada" en un
    /// costo continuo, y le da a cada rodillo roto una recompensa inmediata en vez de una que
    /// aparece recién al final de la cuenta.
    /// </para>
    /// <para>
    /// <b>Se paga sólo por lo que se puede romper.</b> El rodillo trabado por
    /// <c>AINode_LockReel</c> (Fase 2) queda fuera del conteo: es inrompible por diseño, y cobrar
    /// por algo que el jugador no tiene forma de contestar deja de ser una mecánica y pasa a ser un
    /// impuesto. Es la misma razón por la que <see cref="Cap"/> existe — ver abajo.
    /// </para>
    /// <para>
    /// <b>Por qué hay techo.</b> El jugador regenera <c>EnergyRegenBase</c> (2) por turno sobre un
    /// <c>EnergyMax</c> de 4. Con los tres rodillos armados y sin techo el drenaje sería 3 contra un
    /// regen de 2: energía neta negativa para siempre, o sea un candado. Con el techo en 2 el peor
    /// caso empata el regen — el jugador pierde el reroll pago y parte de su margen, pero nunca
    /// entra en una espiral de la que no puede salir. El kit del jugador no se toca: acá se drena el
    /// recurso, no se reescriben sus reglas.
    /// </para>
    /// <para>
    /// <b>Fases por el árbol, no por estado.</b> Fase 1 y Fase 2 son dos instancias de este nodo con
    /// <see cref="Cap"/> distinto bajo un <c>AINode_If(PcOwnerHpBelow)</c> — mismo criterio que
    /// <c>AINode_RotateBlock</c>. Sin mutación en runtime, y el árbol dice cuánto cobra cada fase.
    /// </para>
    /// <para>
    /// <b>Siempre <see cref="AIResult.Succeeded"/>.</b> "No hay rodillos vivos" y "el jugador está
    /// seco" son resoluciones válidas del peaje, no fallos: un <c>Failed</c> acá le cortaría al jefe
    /// el resto del turno —la reposición de la fila y el pool de acción incluidos— por no haber
    /// tenido nada que cobrar.
    /// </para>
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

        /// <summary>
        /// Cobra de a uno hasta <paramref name="amount"/> o hasta que el jugador quede seco.
        /// </summary>
        /// <remarks>
        /// <c>SpendEnergy</c> es todo-o-nada —devuelve <c>false</c> sin mutar si <c>cost &gt;
        /// current</c>— así que pedir los dos de una dejaría al jugador con 1 de energía pagando
        /// cero. De a uno el peaje cobra lo que hay, que es lo que un peaje hace. Es además el path
        /// canónico para restar energía: mutar el atributo a mano se saltea el payload
        /// <c>(current, max)</c> que el HUD necesita para repintarse.
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

        /// <summary>
        /// Número flotante sobre el jugador + el manotazo de la máquina.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b><see cref="FloatingNumberType.Status"/> y no <c>Damage</c>.</b> El jugador pierde
        /// energía, no vida. Un número rojo de daño sobre su cabeza en un turno en que la barra de
        /// vida no se movió le enseña a desconfiar de los números, que es peor que no mostrar nada.
        /// </para>
        /// <para>
        /// <b>No bloquea el turno.</b> Se pide sin <c>BeginFeedbackWait</c>: es un cobro pasivo que
        /// pasa todos los turnos, y frenar la pelea un segundo cada vez lo vuelve un impuesto al
        /// ritmo en vez de una lectura. Mismo criterio que
        /// <c>CashierCounterTollService.PlayTollFeedback</c>.
        /// </para>
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
