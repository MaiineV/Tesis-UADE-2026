namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Nodo que además de su turno tiene algo que dejar puesto <b>antes</b> del primer turno del
    /// jugador: la mesa en el piso, el peaje armado, el dado confiscado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existe porque el estado de sala de un jefe no es estado de sala: está autorado como nodos de
    /// acción dentro de su árbol, y el árbol sólo tickea en el turno del jefe. Como la cola pone al
    /// jugador en el índice 0 sin condición (CNF-006), su primer turno se jugaba contra una sala
    /// vacía y todo aparecía junto al cerrarlo.
    /// </para>
    /// <para>
    /// Es un opt-in y no un barrido del árbol entero a propósito: la apertura instala amenaza, no la
    /// ejecuta. Un ataque que corriera acá sería daño antes de que el jugador toque un dado.
    /// </para>
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
