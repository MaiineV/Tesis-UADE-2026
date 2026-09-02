using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// El árbol de un enemigo leído de afuera y sin tickearlo: lo que ya tiene en curso y lo que
    /// va a hacer en su próximo turno.
    /// </summary>
    public interface IEnemyIntentService
    {
        /// <summary>
        /// Llena <paramref name="standing"/> con lo que el enemigo ya tiene puesto,
        /// <paramref name="next"/> con su siguiente ataque y <paramref name="options"/> con
        /// todos los tiempos de su ciclo — el repertorio entero, para que el panel pueda listar
        /// los ataques posibles y no sólo el que viene. Las listas se limpian antes.
        /// </summary>
        /// <remarks>
        /// Devuelve <c>false</c> fuera del turno del jugador: durante el turno del enemigo el
        /// índice de su ciclo ya avanzó y sus marcas están en movimiento, así que lo que se lea
        /// ahí no es una predicción sino una foto a medio revelar.
        /// </remarks>
        bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next,
                     List<AIIntent> options = null);

        /// <summary>
        /// Llena <paramref name="into"/> con el alcance del arma del enemigo: las celdas desde
        /// las que su ataque pega, medidas desde donde está parado y sin contar su movimiento
        /// (ver <see cref="EnemyAttackReach"/>). El set se limpia antes.
        /// </summary>
        /// <remarks>
        /// Mismo gate temporal que <see cref="TryRead"/>. Tiene default para que un lector
        /// parcial (fakes, paneles que no pintan piso) no deba saber de alcances.
        /// </remarks>
        bool TryReadReach(Guid enemyId, HashSet<GridCoord> into) => false;
    }
}
