using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// El árbol de un enemigo leído de afuera y sin tickearlo: lo que ya tiene en curso y lo que
    /// va a hacer en su próximo turno.
    /// </summary>
    public interface IEnemyIntentService
    {
        /// <summary>
        /// Llena <paramref name="standing"/> con lo que el enemigo ya tiene puesto y
        /// <paramref name="next"/> con su siguiente ataque. Ambas listas se limpian antes.
        /// </summary>
        /// <remarks>
        /// Devuelve <c>false</c> fuera del turno del jugador: durante el turno del enemigo el
        /// índice de su ciclo ya avanzó y sus marcas están en movimiento, así que lo que se lea
        /// ahí no es una predicción sino una foto a medio revelar.
        /// </remarks>
        bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next);
    }
}
