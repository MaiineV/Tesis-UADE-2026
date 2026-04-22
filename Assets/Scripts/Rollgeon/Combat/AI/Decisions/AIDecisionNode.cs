using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Base polimorfica de los nodos del AI tree. TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// Serializada via <c>[SerializeReference]</c> en <c>EnemyDataSO.AIRoot</c> + Odin
    /// para inspector autorable. Ver regla §13.6.1 (similar a <c>BaseTargetQuery</c>).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public abstract class AIDecisionNode
    {
        public virtual string NodeName => GetType().Name;

        public abstract AIResult Tick(AIContext context);
    }
}
