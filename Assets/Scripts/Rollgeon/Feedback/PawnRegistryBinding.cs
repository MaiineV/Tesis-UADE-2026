using System;
using Patterns;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Pegar este <c>MonoBehaviour</c> al GameObject visual de un pawn para registrarlo
    /// en <see cref="IPawnRegistry"/> al <c>OnEnable</c>. Se desregistra en <c>OnDisable</c>.
    /// </summary>
    /// <remarks>
    /// El <see cref="EntityGuid"/> normalmente lo setea una capa superior (combat spawner /
    /// floor loader) justo después de instanciar el prefab. Como fallback dev, se puede
    /// setear a mano desde el inspector con <c>TryParse</c> — ver <see cref="SetGuid(Guid)"/>.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PawnRegistryBinding : MonoBehaviour
    {
        [SerializeField, Tooltip("Solo para debug dev. En runtime normal, se setea por código.")]
        private string _entityGuidString;

        private Guid _guid;
        private bool _registered;

        public Guid EntityGuid => _guid;

        public void SetGuid(Guid guid)
        {
            if (_registered && _guid != Guid.Empty && _guid != guid)
                UnregisterInternal();

            _guid = guid;
            _entityGuidString = guid.ToString();
            TryRegister();
        }

        private void OnEnable()
        {
            if (_guid == Guid.Empty && !string.IsNullOrEmpty(_entityGuidString)
                && Guid.TryParse(_entityGuidString, out var parsed))
            {
                _guid = parsed;
            }
            TryRegister();
        }

        private void OnDisable() => UnregisterInternal();

        private void TryRegister()
        {
            if (_registered || _guid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IPawnRegistry>(out var reg) || reg == null) return;
            reg.Register(_guid, transform);
            _registered = true;
        }

        private void UnregisterInternal()
        {
            if (!_registered) return;
            if (ServiceLocator.TryGetService<IPawnRegistry>(out var reg) && reg != null)
                reg.Unregister(_guid);
            _registered = false;
        }
    }
}
