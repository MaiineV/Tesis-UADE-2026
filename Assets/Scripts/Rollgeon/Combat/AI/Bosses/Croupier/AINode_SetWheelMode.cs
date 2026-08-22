using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Cambia el modo de mesa del Croupier: cuántos números canta por turno, si la rueda queda trucada
    /// y el <see cref="ICroupierWheelService.PhaseIndex"/> que leen los nodos con valores por fase.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SetWheelMode : AIActionNode
    {
        [Tooltip("Números que canta por turno de acá en adelante. Fase 2 = 2 (dos sectores caen y dos " +
                 "dados se van). El servicio lo clampea al máximo de slots.")]
        [MinValue(1)]
        public int NumbersPerTurn = 2;

        [Tooltip("Rueda trucada: terminar el turno en el sector cantado no lo corre. La Represalia " +
                 "se sigue cobrando igual.")]
        public bool Rigged = true;

        [Tooltip("Índice de fase que leen los nodos con valores por fase (daño de sector, fuego).")]
        [MinValue(1)]
        public int PhaseIndex = 2;

        public override string NodeName => $"Set Wheel Mode (×{NumbersPerTurn}{(Rigged ? ", rigged" : "")})";

        public override AIResult Tick(AIContext context)
        {
            var wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return AIResult.Failed;

            if (context != null && context.SelfGuid != Guid.Empty) wheel.Bind(context.SelfGuid);
            wheel.SetMode(NumbersPerTurn, Rigged, PhaseIndex);
            return AIResult.Succeeded;
        }
    }
}
