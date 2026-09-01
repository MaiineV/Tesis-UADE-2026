namespace Rollgeon.Combat.TurnState
{
    /// <summary>
    /// Estado por turno/combate del JUGADOR que los items pasivos leen vía
    /// readers/precondiciones (Corredor Incansable, Piedra de Guardia, Furia Contenida).
    /// Solo lectura desde afuera — el servicio se alimenta de los eventos del juego.
    /// </summary>
    public interface IPlayerTurnStateService
    {
        /// <summary>
        /// Casillas efectivamente recorridas por el jugador en el turno actual (solo
        /// combate; teleports no cuentan). Se resetea al empezar su turno y, DIFERIDO,
        /// después de un ataque con combo — "solo el ataque que sigue al movimiento".
        /// </summary>
        int TilesMovedThisTurn { get; }

        /// <summary>
        /// Rondas completas consecutivas sin que el jugador PIERDA vida en el combate
        /// actual (daño 100% absorbido por escudo no corta la racha). Se resetea al
        /// recibir daño y al empezar cada combate.
        /// </summary>
        int CleanTurnStreak { get; }
    }
}
