using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Nodo compuesto con control flow (Sequence, Selector, If, Random). Estructural,
    /// no ejecuta acciones directamente — delega en hijos.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class AIQuestionNode : AIDecisionNode { }
}
