using System.Collections.Generic;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Nodo que sabe decir qué va a hacer antes de hacerlo, para que el jugador lo lea en su turno.
    /// </summary>
    /// <remarks>
    /// Sólo lo implementan los nodos cuya intención se puede afirmar sin adivinar. Un nodo que
    /// <b>anuncia</b> no lo implementa: su forma se ancla al tickear, detrás de la fuga, y un paso
    /// posterior del árbol puede descartarla en el mismo turno. Lo describe el nodo que la
    /// <b>consume</b>, que la lee ya congelada del <c>IThreatenedAreaService</c>.
    /// </remarks>
    public interface IAIIntentNode
    {
        /// <returns><c>false</c> = "no aplica ahora". Nunca una estimación.</returns>
        bool TryDescribeIntent(AIContext context, out AIIntent intent);

        /// <summary>
        /// Para los nodos que describen varias cosas a la vez — una cruz por bomba. El default es
        /// la única que haya.
        /// </summary>
        void DescribeIntents(AIContext context, List<AIIntent> into)
        {
            if (into != null && TryDescribeIntent(context, out var one)) into.Add(one);
        }
    }
}
