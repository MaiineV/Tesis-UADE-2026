using System;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// Casillas que el jugador recorrió en el movimiento voluntario que disparó el hook
    /// <c>PlayerMoved</c> × <see cref="Multiplier"/>, con tope opcional POR TURNO
    /// (<see cref="CapPerTurn"/>). Es el reader de "Baluarte móvil" (+1 escudo por casilla,
    /// máx. 6 por turno) y de cualquier encantamiento "por casilla recorrida" del GDD.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tope sin estado.</b> El contexto trae el acumulado del turno
    /// (<c>TilesTraversedThisTurn</c>, este movimiento incluido), así que la parte de este
    /// movimiento que entra bajo el tope es
    /// <c>min(total, cap) − min(total − casillas, cap)</c>: no hace falta counter ni reset.
    /// </para>
    /// <para>
    /// <b>Stacking GDD.</b> Varias copias del mismo encantamiento en el dado no duplican el
    /// grant: solo la primera copia viva (menor índice) lee; las demás devuelven 0 y cada
    /// una suma <see cref="CapPerExtraCopy"/> al tope.
    /// </para>
    /// Devuelve 0 fuera del hook (sin casillas) o sin trigger context con Slot.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadTilesTraversed : EffectIntReader
    {
        [Tooltip("Valor por casilla recorrida.")]
        public int Multiplier = 1;

        [Tooltip("Tope de casillas que cuentan por turno. 0 = sin tope.")]
        [MinValue(0)]
        public int CapPerTurn = 0;

        [Tooltip("Cuánto sube el tope por cada copia extra del mismo encantamiento en el dado. " +
                 "Solo aplica con CapPerTurn > 0.")]
        [MinValue(0)]
        public int CapPerExtraCopy = 0;

        public override int Read(EffectContext context)
        {
            if (context == null) return 0;
            if (!context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return 0;

            int tiles = trig.TilesTraversed;
            if (tiles <= 0) return 0;
            if (CapPerTurn <= 0) return tiles * Multiplier;

            var slot = trig.Slot.Value;
            int copies = MovementLaneCopies.Count(slot, out bool isFirstCopy);
            if (!isFirstCopy) return 0;

            int cap = CapPerTurn + CapPerExtraCopy * Math.Max(0, copies - 1);
            int total = Math.Max(tiles, trig.TilesTraversedThisTurn);
            int before = total - tiles;
            int counted = Math.Min(total, cap) - Math.Min(before, cap);
            return Math.Max(0, counted) * Multiplier;
        }
    }
}
