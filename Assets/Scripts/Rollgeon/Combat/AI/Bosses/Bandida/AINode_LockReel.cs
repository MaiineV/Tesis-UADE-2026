using System;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// HOLD (Fase 2): traba una ranura de la fila. El rodillo trabado deja de cancelar la cuenta y
    /// se vuelve inrompible, así que quedan dos blancos válidos — los dos de la punta, los que
    /// están más lejos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va dentro del <c>Once</c> del gate de fase: es un one-shot. Devuelve
    /// <see cref="AIResult.Failed"/> si la fila todavía no está armada, para que el <c>Once</c> no
    /// latchee en falso y lo reintente al turno siguiente.
    /// </para>
    /// <para>
    /// <b>Invulnerabilidad = pool de vida inagotable.</b> El <c>DamagePipeline</c> no expone un
    /// canal de inmunidad y agregarlo sería cambiar una fundación. Con <see cref="LockedHp"/> muy
    /// por encima del techo de daño del jugador el rodillo no se rompe en toda la pelea, y su barra
    /// se queda visualmente llena — que es exactamente cómo se lee "trabado".
    /// </para>
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
            // repone (lee Slot.Locked). Acá solo blindamos al rodillo que ya está en la fila.
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].Side != Side || !slots[i].IsAlive) continue;
                context.Attributes?.SetAttributeValue<Health, int>(slots[i].ReelGuid, LockedHp);
            }

            return AIResult.Succeeded;
        }
    }
}
