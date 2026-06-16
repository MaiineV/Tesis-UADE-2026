using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Nodo hoja que ejecuta una accion concreta (ataque, movimiento, espera, etc.).
    /// Convención: Tick retorna <see cref="AIResult.Succeeded"/> si la acción se ejecutó
    /// y <see cref="AIResult.Failed"/> si no (ej. sin target, fuera de rango).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class AIActionNode : AIDecisionNode { }
}
