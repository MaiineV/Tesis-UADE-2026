using System;
using System.Collections.Generic;
using Rollgeon.Attributes;

namespace Rollgeon.Combat.Initiative
{
    /// <summary>
    /// Implementación trivial de <see cref="IEntityRegistry"/> respaldada por
    /// un <see cref="Dictionary{TKey, TValue}"/>. Destinada a tests y a smoke
    /// checks en scenes de prueba (ver <c>docs/setup/System#0100c_...</c>).
    /// </summary>
    /// <remarks>
    /// [STUB] — desaparece cuando el worktree de Entities ofrezca un registro
    /// real. La interfaz se mantiene; sólo cambia la implementación registrada
    /// por el bootstrap.
    /// </remarks>
    public sealed class InMemoryEntityRegistry : IEntityRegistry
    {
        private readonly Dictionary<Guid, ModifiableAttributes> _byId
            = new Dictionary<Guid, ModifiableAttributes>();

        /// <summary>Registra (o sobrescribe) los atributos de una entidad.</summary>
        public void Register(Guid entityId, ModifiableAttributes attrs)
        {
            if (entityId == Guid.Empty)
            {
                throw new ArgumentException("entityId cannot be Guid.Empty", nameof(entityId));
            }
            if (attrs == null)
            {
                throw new ArgumentNullException(nameof(attrs));
            }
            _byId[entityId] = attrs;
        }

        /// <summary>Quita la entry — no lanza si no existía.</summary>
        public void Unregister(Guid entityId)
        {
            _byId.Remove(entityId);
        }

        /// <summary>Vacía el registro.</summary>
        public void Clear()
        {
            _byId.Clear();
        }

        /// <inheritdoc />
        public bool TryGetAttributes(Guid entityId, out ModifiableAttributes attrs)
        {
            return _byId.TryGetValue(entityId, out attrs);
        }
    }
}
