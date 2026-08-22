using System;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// HOLD (Fase 2): traba una ranura de la fila — su rodillo deja de cancelar la cuenta y se
    /// vuelve inrompible.
    /// </summary>
    /// <remarks>
    /// Devuelve <see cref="AIResult.Failed"/> si la fila todavía no está armada, para que el
    /// <c>Once</c> que lo envuelve no se consuma y lo reintente al turno siguiente. La
    /// invulnerabilidad se simula con vida inagotable: el <c>DamagePipeline</c> no expone un canal
    /// de inmunidad.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_LockReel : AIActionNode
    {
        [Tooltip("Ranura a trabar. Middle = el rodillo del medio (el más cercano al jugador).")]
        public ReelSide Side = ReelSide.Middle;

        [Tooltip("Vida con la que queda el rodillo trabado. Muy por encima del techo de daño del " +
                 "jugador ⇒ inrompible en la práctica.")]
        [MinValue(1)]
        public int LockedHp = 999;

        public override string NodeName => $"Lock Reel ({Side})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var service = BandidaJackpotService.ResolveOrCreate();
            service.BindBoss(context.SelfGuid);

            var slots = service.Slots;
            if (slots.Count == 0) return AIResult.Failed;

            service.LockSlot(Side, LockedHp);

            // Si la ranura está rota justo ahora, el HOLD se aplica cuando AINode_SpawnReels la
            // repone (lee Slot.Locked).
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Side != Side || !slots[i].IsAlive) continue;
                context.Attributes?.SetAttributeValue<Health, int>(slots[i].ReelGuid, LockedHp);
            }

            return AIResult.Succeeded;
        }
    }
}
