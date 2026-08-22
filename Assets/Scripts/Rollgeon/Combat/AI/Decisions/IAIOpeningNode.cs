namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Nodo que además de su turno tiene algo que dejar puesto <b>antes</b> del primer turno del
    /// jugador: la mesa en el piso, el peaje armado, el dado confiscado.
    /// </summary>
    /// <remarks>
    /// El estado de sala de un jefe está autorado como nodos de acción de su árbol, y el árbol sólo
    /// tickea en el turno del jefe, que va después del del jugador (CNF-006). Sólo lo implementan
    /// los nodos que instalan amenaza: un ataque que corriera acá sería daño antes de que el jugador
    /// toque un dado.
    /// </remarks>
    public interface IAIOpeningNode
    {
        /// <summary>
        /// Deja instalado lo que el nodo necesita tener de entrada. Corre una sola vez por pelea, en
        /// orden de árbol, y <b>no</b> reemplaza al <see cref="AIDecisionNode.Tick"/> del turno del
        /// jefe: el nodo va a volver a correr completo cuando le toque.
        /// </summary>
        void Opening(AIContext context);
    }
}
