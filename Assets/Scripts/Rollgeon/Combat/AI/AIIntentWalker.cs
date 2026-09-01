using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// El árbol leído de afuera sin tickearlo: lo que el enemigo ya tiene en curso y lo que va a
    /// hacer en su próximo turno.
    /// </summary>
    /// <remarks>
    /// Mismo criterio que <c>TreeDrivenEnemyAI.CollectOpeningNodes</c>, con una diferencia: un
    /// <see cref="AINode_Alternate"/> no aporta todos sus hijos sino <b>sólo el que le toca</b>.
    /// Lo que no se entiende no se abre — un nodo que no sabe describirse no se adivina.
    /// </remarks>
    public static class AIIntentWalker
    {
        /// <summary>
        /// Llena <paramref name="standing"/> con los pasos que la raíz tickea todos los turnos,
        /// <paramref name="next"/> con el próximo tiempo del ciclo, y <paramref name="options"/>
        /// con TODOS los tiempos del ciclo — el repertorio, no la agenda. Las listas se limpian.
        /// </summary>
        public static void Collect(AIDecisionNode root, AIContext context,
                                   List<AIIntent> standing, List<AIIntent> next,
                                   List<AIIntent> options = null)
        {
            standing?.Clear();
            next?.Clear();
            options?.Clear();
            if (root == null || context == null) return;

            var alternates = new List<AINode_Alternate>();
            Walk(root, context, standing, alternates);

            foreach (var alternate in alternates)
            {
                if (next != null) Walk(alternate.NextChild, context, next, null);

                // El repertorio se describe con TryDescribeOption y no con la intención viva:
                // el cono sin marca pendiente y el disparo fuera de rango contestan false, que
                // es lo correcto para "qué va a pasar" y lo incorrecto para "qué sabe hacer".
                if (options == null || alternate.Children == null) continue;
                foreach (var child in alternate.Children)
                    Walk(child, context, options, null, asOptions: true);
            }
        }

        /// <summary>
        /// Todos los nodos de un tipo que cuelgan del árbol, sin tickearlo ni elegir rama.
        /// </summary>
        /// <remarks>
        /// A diferencia de <see cref="Collect"/> baja por las <b>dos</b> ramas de un <c>If</c> y por
        /// todos los hijos de un <c>Alternate</c>: no pregunta qué va a hacer ahora sino qué es
        /// capaz de hacer, que es lo que necesita quien lo describe fuera de combate. Y por eso no
        /// pide contexto: nada acá evalúa una condición.
        /// </remarks>
        public static void CollectNodes<T>(AIDecisionNode node, List<T> into)
            where T : AIDecisionNode
        {
            if (node == null || into == null) return;
            if (node is T match) into.Add(match);

            switch (node)
            {
                case AINode_Sequence sequence:
                    CollectChildren(sequence.Children, into);
                    break;

                case AINode_Selector selector:
                    CollectChildren(selector.Children, into);
                    break;

                case AINode_Alternate alternate:
                    CollectChildren(alternate.Children, into);
                    break;

                case AINode_If branch:
                    CollectNodes(branch.Then, into);
                    CollectNodes(branch.Else, into);
                    break;

                case AINode_Once once:
                    CollectNodes(once.Child, into);
                    break;

                case AINode_While loop:
                    CollectNodes(loop.Body, into);
                    break;
            }
        }

        private static void CollectChildren<T>(List<AIDecisionNode> children, List<T> into)
            where T : AIDecisionNode
        {
            if (children == null) return;
            foreach (var child in children) CollectNodes(child, into);
        }

        private static void Walk(AIDecisionNode node, AIContext context,
                                 List<AIIntent> into, List<AINode_Alternate> alternates,
                                 bool asOptions = false)
        {
            if (node == null) return;

            if (node is IAIIntentNode intentNode && into != null)
            {
                if (asOptions)
                {
                    if (intentNode.TryDescribeOption(context, out var option)) into.Add(option);
                }
                else
                {
                    intentNode.DescribeIntents(context, into);
                }
            }

            switch (node)
            {
                case AINode_Sequence sequence:
                    WalkChildren(sequence.Children, context, into, alternates, asOptions);
                    break;

                case AINode_Selector selector:
                    WalkChildren(selector.Children, context, into, alternates, asOptions);
                    break;

                // En la pasada de "lo que está en curso" el ciclo no aporta nada: sus hijos son
                // alternativas de turnos distintos, y meterlos todos diría que el jefe hace las
                // tres cosas a la vez. Se anota para leerle SOLO el próximo después.
                case AINode_Alternate alternate:
                    alternates?.Add(alternate);
                    break;

                // La rama que el propio If elegiría, preguntándole a él y no reimplementando la
                // condición acá. Como repertorio se abren las DOS: la condición es el estado de
                // este turno, y el duplicado que resulta lo filtra por key quien arma la columna.
                case AINode_If branch:
                    if (asOptions)
                    {
                        Walk(branch.Then, context, into, alternates, asOptions);
                        Walk(branch.Else, context, into, alternates, asOptions);
                    }
                    else
                    {
                        Walk(branch.Evaluate(context) ? branch.Then : branch.Else,
                             context, into, alternates);
                    }
                    break;

                // Un Once ya latcheado es transparente y su hijo no vuelve a correr — también
                // como repertorio: lo que ya se gastó dejó de ser posible.
                case AINode_Once once:
                    if (!once.HasRun) Walk(once.Child, context, into, alternates, asOptions);
                    break;

                // El While es presupuesto, no bifurcación: su cuerpo ES lo que el enemigo hace
                // (todo el bestiario común envuelve su ataque en un While de energía). Su condición
                // no se evalúa acá: mide el estado DURANTE el turno del enemigo, y leída en el
                // turno del jugador contestaría por un momento que todavía no llegó.
                case AINode_While loop:
                    Walk(loop.Body, context, into, alternates, asOptions);
                    break;
            }
        }

        private static void WalkChildren(List<AIDecisionNode> children, AIContext context,
                                         List<AIIntent> into, List<AINode_Alternate> alternates,
                                         bool asOptions = false)
        {
            if (children == null) return;
            foreach (var child in children) Walk(child, context, into, alternates, asOptions);
        }
    }
}
