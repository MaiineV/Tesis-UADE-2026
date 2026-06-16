using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Aplica un cambio de stats <b>permanente</b> al propio Boss (Sistemas prerequisito Bosses §6;
    /// decisión de diseño: cambio real vía modifier permanente). Pensado para envolverse en
    /// <see cref="AINode_Once"/> bajo un <c>AINode_If(PcOwnerHpBelow)</c> para disparar Fase 2 una
    /// sola vez al cruzar el umbral de HP. Emite <see cref="EventName.OnBossPhaseChanged"/> para
    /// que el feedback visual + diálogo (wireado en engine) reaccione.
    /// </summary>
    /// <remarks>
    /// <b>Velocidad.</b> El cambio de <c>Speed</c> reordena la cola de turnos recién en la próxima
    /// ronda (la cola se arma por ronda con initiative); <c>Attack</c> es inmediato. El modifier es
    /// <see cref="ModifierLifetime.Permanent"/> — no se revierte si el HP vuelve a subir.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ApplyStatModifier : AIActionNode
    {
        [Tooltip("Delta permanente al stat Attack (fuerza). 0 = no toca Attack.")]
        public int AttackDelta = 0;

        [Tooltip("Delta permanente al stat Speed (velocidad / posición en cola). 0 = no toca Speed.")]
        public int SpeedDelta = 0;

        [Tooltip("Índice de fase (1-based) que se reporta en OnBossPhaseChanged. Fase 2 = 2.")]
        [MinValue(1)]
        public int PhaseIndex = 2;

        [Tooltip("Si true, dispara OnBossPhaseChanged(selfGuid, PhaseIndex) para feedback/diálogo.")]
        public bool EmitPhaseChangedEvent = true;

        public override string NodeName => $"Apply Stat Modifier (ATK {AttackDelta:+0;-0;0}, SPD {SpeedDelta:+0;-0;0})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var attrs = context.Attributes;
            if (attrs == null) ServiceLocator.TryGetService<AttributesManager>(out attrs);
            if (attrs == null)
            {
                Debug.LogWarning("[AINode_ApplyStatModifier] AttributesManager no disponible — no se aplica el cambio de stats.");
                return AIResult.Failed;
            }

            if (AttackDelta != 0) AddPermanent<Attack>(attrs, context.SelfGuid, AttackDelta);
            if (SpeedDelta != 0) AddPermanent<Speed>(attrs, context.SelfGuid, SpeedDelta);

            if (EmitPhaseChangedEvent)
                EventManager.Trigger(EventName.OnBossPhaseChanged, context.SelfGuid, PhaseIndex);

            return AIResult.Succeeded;
        }

        private static void AddPermanent<TAttribute>(AttributesManager attrs, Guid guid, int delta)
            where TAttribute : class, IModifiable<int>
        {
            var modifier = new Modifier<int>(
                amount: delta,
                op: ModifierOperation.Add,
                duration: 0, // ignorado: lifetime Permanent no tickea
                carrierId: guid,
                sourceId: guid,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Permanent,
                tickEvent: EventName.OnTurnFinished);
            attrs.AddModifier<TAttribute, int>(guid, modifier);
        }
    }
}
