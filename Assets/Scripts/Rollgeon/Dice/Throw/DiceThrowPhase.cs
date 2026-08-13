namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Fase GLOBAL de la sesión de throw — resumen derivado de los
    /// <see cref="DieThrowState"/> por dado (ver <see cref="DiceThrowStateMachine.Phase"/>).
    /// </summary>
    public enum DiceThrowPhase
    {
        /// <summary>Sin sesión activa (o modo instantáneo).</summary>
        Idle,

        /// <summary>Sesión abierta, dados esperando el grab del jugador.</summary>
        Pending,

        /// <summary>Al menos un dado agarrado siguiendo al cursor.</summary>
        Grabbing,

        /// <summary>Al menos un dado en vuelo.</summary>
        Flying,

        /// <summary>Todos los dados participantes asentados — falta el alineado + reveal.</summary>
        Settled,
    }
}
