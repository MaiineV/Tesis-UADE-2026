using System;

namespace Rollgeon.Tiles
{
    /// <summary>
    /// Momentos de activación de una casilla especial (GDD, sección 6). Máscara: una
    /// casilla puede reaccionar a varios (Fuego = OnEnter | OnTurnStart).
    /// </summary>
    /// <remarks>
    /// Los valores desde <see cref="OnExit"/> están <b>reservados</b>: declarados para que
    /// agregar su dispatch no requiera tocar el enum ni migrar data, pero hoy ningún camino
    /// del motor los evalúa.
    /// </remarks>
    [Flags]
    public enum TileTrigger
    {
        None = 0,

        // --- Activos ----------------------------------------------------------
        /// <summary>El recorrido de la unidad incluye la casilla, se detenga ahí o no.</summary>
        OnEnter = 1 << 0,
        /// <summary>Subcaso de OnEnter donde el movimiento continúa (Hielo, Portal). Una
        /// entrada cualquiera satisface OnEnter y OnPassThrough por igual.</summary>
        OnPassThrough = 1 << 1,
        /// <summary>Inicio del turno de la unidad, según la casilla que ocupa (Fuego).</summary>
        OnTurnStart = 1 << 2,
        /// <summary>La unidad termina su turno sobre la casilla (Curación).</summary>
        OnEndTurn = 1 << 3,
        /// <summary>Estado continuo mientras permanece — no es un pulso; se consulta
        /// on-demand (Fortaleza, Zona de Seguridad).</summary>
        OnRemainOn = 1 << 4,
        /// <summary>El empuje cruza la unidad por la casilla (Pinchos, Portal, Charco).</summary>
        OnForcedMovementInto = 1 << 5,
        /// <summary>Al resolver la tirada de movimiento estando sobre la casilla (Impulso —
        /// inerte hasta que exista tirada real).</summary>
        OnMovementRoll = 1 << 6,
        /// <summary>Vence el tiempo de advertencia (Telegraph).</summary>
        OnTelegraphExpire = 1 << 7,

        // --- Reservados (sin dispatch todavía) ---------------------------------
        OnExit = 1 << 8,
        OnRoundStart = 1 << 9,
        OnRoundEnd = 1 << 10,
        OnReceiveDamage = 1 << 11,
        OnAttackFromTile = 1 << 12,
        OnSpawn = 1 << 13,
        OnDestroy = 1 << 14,
    }
}
