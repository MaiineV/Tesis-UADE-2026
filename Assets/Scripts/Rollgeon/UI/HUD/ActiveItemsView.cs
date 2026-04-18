using System;
using System.Collections.Generic;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Contenedor de slots de items activos (arco, pocion, ...). Se suscribe a
    /// <see cref="EventName.OnItemObtained"/>, <see cref="EventName.OnActiveItemUsed"/>,
    /// <see cref="EventName.OnItemRemoved"/> y dispara el <see cref="ActiveItemSlotView.SetState"/>
    /// correspondiente segun el itemId.
    /// </summary>
    /// <remarks>
    /// Plan §4.6. El numero de slots y sus <c>ItemId</c> se configura en Inspector
    /// via <see cref="_bindings"/>. Si un <c>OnItemObtained</c> llega con un id que
    /// no esta en <c>_bindings</c> se ignora silenciosamente (item no-active o sin slot).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Active Items View")]
    public class ActiveItemsView : MonoBehaviour
    {
        private const string LogPrefix = "[ActiveItemsView] ";

        /// <summary>
        /// Mapping inspector-configurable entre <c>ItemId</c> (catalog string) y el
        /// <see cref="ActiveItemSlotView"/> que lo representa en pantalla.
        /// </summary>
        [Serializable]
        public struct ItemSlotBinding
        {
            [Tooltip("Id del item en el catalogo. Ej: 'item.arco', 'item.pocion'.")]
            public string ItemId;

            [Tooltip("Slot view que representa este item en pantalla.")]
            public ActiveItemSlotView Slot;
        }

        [Title("Active Items — Slot bindings")]
        [InfoBox("Cada entrada mapea un ItemId del catalogo al ActiveItemSlotView " +
                 "que lo representa. Sin bindings, los eventos se ignoran sin crashear.")]
        [SerializeField]
        private List<ItemSlotBinding> _bindings = new List<ItemSlotBinding>();

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            _playerGuid = playerGuid;
            EventManager.Subscribe(EventName.OnItemObtained, HandleItemObtained);
            EventManager.Subscribe(EventName.OnActiveItemUsed, HandleActiveItemUsed);
            EventManager.Subscribe(EventName.OnItemRemoved, HandleItemRemoved);
            _bound = true;

            FetchInitialState();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleItemObtained);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleActiveItemUsed);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleItemRemoved);
            _bound = false;
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        private void HandleItemObtained(params object[] args)
        {
            if (!TryReadGuidAndItemId(args, out var guid, out var itemId)) return;
            if (guid != _playerGuid) return;

            if (TryFindSlot(itemId, out var slot))
            {
                slot.SetState(ActiveItemState.Active);
            }
            // else: item legitimo no-active / sin slot HUD — se ignora sin warning.
        }

        private void HandleActiveItemUsed(params object[] args)
        {
            if (!TryReadGuidAndItemId(args, out var guid, out var itemId)) return;
            if (guid != _playerGuid) return;

            if (TryFindSlot(itemId, out var slot))
            {
                slot.SetState(ActiveItemState.Depleted);
            }
        }

        private void HandleItemRemoved(params object[] args)
        {
            if (!TryReadGuidAndItemId(args, out var guid, out var itemId)) return;
            if (guid != _playerGuid) return;

            if (TryFindSlot(itemId, out var slot))
            {
                slot.SetState(ActiveItemState.Inactive);
            }
        }

        private static bool TryReadGuidAndItemId(object[] args, out Guid guid, out string itemId)
        {
            guid = Guid.Empty;
            itemId = null;

            if (args == null || args.Length < 2)
            {
                Debug.LogWarning(LogPrefix + "Item event args malformed (len < 2).");
                return false;
            }
            if (!(args[0] is Guid g))
            {
                Debug.LogWarning(LogPrefix + "Item event args[0] is not Guid.");
                return false;
            }
            if (!(args[1] is string s))
            {
                Debug.LogWarning(LogPrefix + "Item event args[1] is not string.");
                return false;
            }
            guid = g;
            itemId = s;
            return true;
        }

        private bool TryFindSlot(string itemId, out ActiveItemSlotView slot)
        {
            // O(N) linear scan — N = 2-6 slots en la practica.
            for (int i = 0; i < _bindings.Count; i++)
            {
                var b = _bindings[i];
                if (b.Slot != null && string.Equals(b.ItemId, itemId, StringComparison.Ordinal))
                {
                    slot = b.Slot;
                    return true;
                }
            }
            slot = null;
            return false;
        }

        /// <summary>
        /// [SEED] Lectura one-shot del inventario inicial (plan §2.4). No existe todavia
        /// <c>IInventoryService.GetActiveItems</c>; los slots arrancan en Inactive y se
        /// rellenan con los eventos que dispare el publisher canonico.
        /// </summary>
        // [STUB] Item catalog — replace hardcoded item IDs with ItemSO refs when F#0010 available.
        // [STUB] OnPlayerStatsSnapshot — remove FetchInitialState when snapshot event exists.
        private void FetchInitialState()
        {
            // Default: todos los slots a Inactive. El diseñador define el estado en prefab;
            // al bindear lo reseteamos para no mostrar estados stale de un run anterior.
            for (int i = 0; i < _bindings.Count; i++)
            {
                var slot = _bindings[i].Slot;
                if (slot != null)
                {
                    slot.SetState(ActiveItemState.Inactive);
                }
            }
        }
    }
}
