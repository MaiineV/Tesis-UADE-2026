using System.Collections.Generic;

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

        /// <summary>
        /// Combos DISTINTOS al anterior encadenados en el combate actual (Mosaico Errático):
        /// el primer combo vale 0, cada combo distinto al último suma 1, repetir el último
        /// vuelve a 0. Se actualiza SINCRÓNICAMENTE dentro del dispatch de ComboPlayed
        /// (antes que los hooks de items), así el combo en curso ya cuenta. Solo acciones
        /// combat-payable (ataque/defensa/cura); movimiento no cuenta.
        /// </summary>
        int ComboVarietyStreak { get; }

        /// <summary>
        /// Ataques con combo YA ejecutados en el combate actual, SIN contar el que está en
        /// curso (Eco Menguante): el commit es diferido al próximo ComboPlayed, así que leído
        /// dentro del dispatch del primer ataque vale 0. Se resetea al empezar cada combate.
        /// </summary>
        int AttacksPlayedThisCombat { get; }

        /// <summary>
        /// Historial de combos de combate (ataque/defensa/cura) jugados en el combate actual,
        /// en orden, INCLUYENDO el combo en curso (append sincrónico dentro del dispatch de
        /// ComboPlayed, antes que los hooks de items). Vértigo cuenta desde el último Par;
        /// Piedra Angular pregunta si es el primero. Vacío fuera de combate.
        /// </summary>
        IReadOnlyList<string> ComboHistoryThisCombat { get; }

        /// <summary>Atajo: <c>ComboHistoryThisCombat.Count</c> (incluye el combo en curso).</summary>
        int CombosPlayedThisCombat { get; }
    }
}
