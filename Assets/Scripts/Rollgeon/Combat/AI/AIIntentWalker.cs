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
        /// Llena <paramref name="standing"/> con los pasos que la raíz tickea todos los turnos y
        /// <paramref name="next"/> con el próximo tiempo del ciclo. Ambas listas se limpian.
        /// </summary>
        public static void Collect(AIDecisionNode root, AIContext context,
                                   List<AIIntent> standing, List<AIIntent> next)
        {
            standing?.Clear();
            next?.Clear();
            if (root == null || context == null) return;

            var alternates = new List<AINode_Alternate>();
            Walk(root, context, standing, alternates);

            if (next == null) return;
            foreach (var alternate in alternates)
                Walk(alternate.NextChild, context, next, null);
        }

        private static void Walk(AIDecisionNode node, AIContext context,
                                 List<AIIntent> into, List<AINode_Alternate> alternates)
        {
            if (node == null) return;

            if (node is IAIIntentNode intentNode && into != null)
                intentNode.DescribeIntents(context, into);

            switch (node)
            {
                case AINode_Sequence sequence:
                    WalkChildren(sequence.Children, context, into, alternates);
                    break;

                case AINode_Selector selector:
                    WalkChildren(selector.Children, context, into, alternates);
                    break;

                // En la pasada de "lo que está en curso" el ciclo no aporta nada: sus hijos son
                // alternativas de turnos distintos, y meterlos todos diría que el jefe hace las
                // tres cosas a la vez. Se anota para leerle SOLO el próximo después.
                case AINode_Alternate alternate:
                    alternates?.Add(alternate);
                    break;

                // La rama que el propio If elegiría, preguntándole a él y no reimplementando la
                // condición acá.
                case AINode_If branch:
                    Walk(branch.Evaluate(context) ? branch.Then : branch.Else, context, into, alternates);
                    break;

                // Un Once ya latcheado es transparente y su hijo no vuelve a correr.
                case AINode_Once once:
                    if (!once.HasRun) Walk(once.Child, context, into, alternates);
                    break;
            }
        }

        private static void WalkChildren(List<AIDecisionNode> children, AIContext context,
                                         List<AIIntent> into, List<AINode_Alternate> alternates)
        {
            if (children == null) return;
            foreach (var child in children) Walk(child, context, into, alternates);
        }
    }
}
