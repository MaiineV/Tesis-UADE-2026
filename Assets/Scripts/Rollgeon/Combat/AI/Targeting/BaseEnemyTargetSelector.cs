using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Targeting
{
    /// <summary>
    /// Algoritmo polimórfico que decide a quién apunta un enemigo. TECHNICAL.md §7.5.
    /// Cualquier nivel del flujo (behavior, EffectData, AINode) puede tener un selector
    /// propio; <see cref="EnemyTargetResolver.Resolve"/> aplica el fallback estándar
    /// (<see cref="TargetSelector_AlwaysPlayer"/>) cuando el selector queda null.
    /// </summary>
    /// <remarks>
    /// Regla §13.6.1: <c>abstract</c> + <c>[Serializable, HideReferenceObjectPicker]</c>.
    /// Los contenedores van con <c>[OdinSerialize, SerializeReference]</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public abstract class BaseEnemyTargetSelector
    {
        public virtual string SelectorName => GetType().Name;

        /// <summary>
        /// Devuelve el guid del target. <see cref="Guid.Empty"/> si no hay candidato válido —
        /// el caller decide si abortar la rama o seguir con un fallback.
        /// </summary>
        public abstract Guid PickTarget(AIContext ctx, Guid ownerGuid);
    }
}
