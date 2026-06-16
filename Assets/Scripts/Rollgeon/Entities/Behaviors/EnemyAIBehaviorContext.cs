using Rollgeon.Combat.AI;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Trigger context que <c>AINode_Behavior</c> arma para invocar a un
    /// <c>EnemyActionBehavior</c> desde el árbol. Lleva el <see cref="AIContext"/>
    /// original para que el behavior tenga acceso a Self/Player guids, registry
    /// services y round, sin re-resolverlos.
    /// </summary>
    public sealed class EnemyAIBehaviorContext : BehaviorContext
    {
        public AIContext AI;
    }
}
