using System;
using System.Collections.Generic;

namespace Rollgeon.Combat.Weakness
{
    /// <summary>
    /// Implementacion default de <see cref="IWeaknessRegistry"/> — diccionario en memoria
    /// <c>Guid → (comboId, multOverride)</c>. Main-thread only (no locking).
    /// <para>
    /// Populated at enemy spawn time por el pipeline de entities (T99). Mientras T99 no
    /// exista, los tests llaman <see cref="SetWeakness"/> directamente.
    /// </para>
    /// </summary>
    public sealed class WeaknessRegistry : IWeaknessRegistry
    {
        private readonly Dictionary<Guid, (string comboId, float mult)> _entries
            = new Dictionary<Guid, (string comboId, float mult)>();

        /// <inheritdoc />
        public void SetWeakness(Guid entityId, string comboId, float multiplierOverride)
        {
            if (entityId == Guid.Empty) return;
            _entries[entityId] = (comboId ?? string.Empty, multiplierOverride);
        }

        /// <inheritdoc />
        public bool TryGet(Guid entityId, out (string comboId, float mult) data)
        {
            if (entityId == Guid.Empty)
            {
                data = (string.Empty, 0f);
                return false;
            }
            return _entries.TryGetValue(entityId, out data);
        }

        /// <inheritdoc />
        public void Unregister(Guid entityId)
        {
            _entries.Remove(entityId);
        }
    }
}
