using System;
using System.Collections.Generic;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view del Combat HUD que renderiza la cola de turnos en orden. Escucha
    /// <see cref="EventName.OnTurnQueueBuilt"/>, <see cref="EventName.OnTurnStarted"/>,
    /// <see cref="EventName.OnTurnFinished"/> y <see cref="EventName.OnEntityDestroyed"/>.
    /// Plan §3.2 / §4.2.
    /// </summary>
    /// <remarks>
    /// Al llegar <c>OnTurnQueueBuilt</c> destruye los slots existentes e instancia un
    /// nuevo <see cref="TurnSlotView"/> por guid (prefab + container inspector).
    /// El mapping <c>guid→slot</c> se mantiene para O(1) highlights.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Turn Queue View")]
    public class TurnQueueView : MonoBehaviour
    {
        private const string LogPrefix = "[TurnQueueView] ";

        [Title("Turn Queue — Widget refs")]
        [Required("Arrastrar el prefab de TurnSlotView (instructivo §8.2).")]
        [SerializeField]
        [Tooltip("Prefab que se instancia una vez por guid en la cola.")]
        private TurnSlotView _slotPrefab;

        [Required("Arrastrar el Transform padre donde se anidan los slots.")]
        [SerializeField]
        [Tooltip("Container de los slots. Tipicamente un HorizontalLayoutGroup.")]
        private Transform _container;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private readonly Dictionary<Guid, TurnSlotView> _slotsByGuid
            = new Dictionary<Guid, TurnSlotView>();

        /// <summary>Suscribe al bus y guarda el playerGuid (solo para highlight del player).</summary>
        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, HandleTurnQueueBuilt);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            _bound = true;
        }

        /// <summary>Desuscribe del bus. Idempotente. Deja los slots vivos (se limpian en OnDisable / re-build).</summary>
        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnTurnQueueBuilt, HandleTurnQueueBuilt);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnEntityDestroyed, HandleEntityDestroyed);
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // API publica (hooks para tests y tooling T95T — plan §7)
        // ======================================================================

        /// <summary>
        /// Re-construye los slots a partir de <paramref name="order"/>. Expuesto publico
        /// para tooling T95T sin tener que disparar el evento.
        /// </summary>
        public void RebuildQueue(IReadOnlyList<Guid> order)
        {
            ClearSlots();
            if (order == null) return;

            if (_slotPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "_slotPrefab no esta cableado — skip rebuild.", this);
                return;
            }
            if (_container == null)
            {
                Debug.LogWarning(LogPrefix + "_container no esta cableado — skip rebuild.", this);
                return;
            }

            for (int i = 0; i < order.Count; i++)
            {
                var guid = order[i];
                var slot = Instantiate(_slotPrefab, _container);
                slot.Bind(guid, guid == _playerGuid, i);
                _slotsByGuid[guid] = slot;
            }
        }

        /// <summary>Destruye todos los slots y limpia el mapping.</summary>
        public void ClearSlots()
        {
            foreach (var kvp in _slotsByGuid)
            {
                var slot = kvp.Value;
                if (slot == null) continue;
                if (Application.isPlaying) Destroy(slot.gameObject);
                else DestroyImmediate(slot.gameObject);
            }
            _slotsByGuid.Clear();
        }

        /// <summary>Lookup del slot por guid. Null si no existe.</summary>
        public TurnSlotView FindSlot(Guid guid)
        {
            _slotsByGuid.TryGetValue(guid, out var slot);
            return slot;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void HandleTurnQueueBuilt(params object[] args)
        {
            if (args == null || args.Length < 1)
            {
                Debug.LogWarning(LogPrefix + "OnTurnQueueBuilt args malformed (len < 1).", this);
                return;
            }
            if (!(args[0] is IReadOnlyList<Guid> order))
            {
                Debug.LogWarning(LogPrefix + "OnTurnQueueBuilt args[0] is not IReadOnlyList<Guid>.", this);
                return;
            }

            RebuildQueue(order);
        }

        private void HandleTurnStarted(params object[] args)
        {
            if (!TryReadGuid(args, "OnTurnStarted", out var guid)) return;
            // Apagar highlights previos + prender el del actor activo.
            foreach (var kvp in _slotsByGuid)
            {
                var slot = kvp.Value;
                if (slot == null) continue;
                slot.SetActive(kvp.Key == guid);
            }
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (!TryReadGuid(args, "OnTurnFinished", out var guid)) return;
            if (_slotsByGuid.TryGetValue(guid, out var slot) && slot != null)
            {
                slot.SetActive(false);
            }
        }

        private void HandleEntityDestroyed(params object[] args)
        {
            if (!TryReadGuid(args, "OnEntityDestroyed", out var guid)) return;
            if (_slotsByGuid.TryGetValue(guid, out var slot) && slot != null)
            {
                slot.SetDestroyed(true);
                slot.SetActive(false);
            }
        }

        private bool TryReadGuid(object[] args, string evName, out Guid guid)
        {
            guid = Guid.Empty;
            if (args == null || args.Length < 1)
            {
                Debug.LogWarning(LogPrefix + evName + " args malformed (len < 1).", this);
                return false;
            }
            if (!(args[0] is Guid g))
            {
                Debug.LogWarning(LogPrefix + evName + " args[0] is not Guid.", this);
                return false;
            }
            guid = g;
            return true;
        }
    }
}
