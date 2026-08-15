using System;
using Rollgeon.Combat.AI.Decisions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Cambia el modo de mesa del Croupier: cuántos números canta por turno y si la rueda queda
    /// trucada. Es el setup de "Pleno y color" (fase 2) — va envuelto en
    /// <c>If(PcOwnerHpBelow) → Once</c>, al lado del <c>ApplyStatModifier</c> que dispara el feedback
    /// de fase.
    /// </summary>
    /// <remarks>
    /// Un solo nodo para las cuatro palancas de la fase en vez de cuatro setters: los valores que
    /// dependen de la fase y no viven en la rueda (daño por sector, duración del fuego) los resuelven
    /// sus propios nodos leyendo <see cref="ICroupierWheelService.PhaseIndex"/>, que se setea acá. Así
    /// hay un único lugar del árbol que decide "estamos en fase 2".
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SetWheelMode : AIActionNode
    {
        [Tooltip("Números que canta por turno de acá en adelante. Fase 2 = 2 (dos sectores caen y dos " +
                 "dados se van). El servicio lo clampea al máximo de slots.")]
        [MinValue(1)]
        public int NumbersPerTurn = 2;

        [Tooltip("Rueda trucada: terminar el turno en el sector cantado ya no lo corre. La Represalia " +
                 "se sigue cobrando — lo que la fase te saca es la palanca, no el precio de pegarle.")]
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
