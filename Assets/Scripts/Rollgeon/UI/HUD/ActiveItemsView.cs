using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Items;
using Rollgeon.Phase;
using Rollgeon.Player;
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
            _playerGuid = playerGuid;
            if (!_bound) Subscribe();
            FetchInitialState();
        }

        public void Unbind()
        {
            // No-op: el ciclo de vida lo controla OnEnable/OnDisable. Sin esto, cuando
            // el HUD de exploration se desactiva al pushear el de combate y vuelve a
            // activarse, los handlers de eventos y los counts quedan stale.
        }

        private void Subscribe()
        {
            if (_bound) return;
            EventManager.Subscribe(EventName.OnItemObtained, HandleItemObtained);
            EventManager.Subscribe(EventName.OnActiveItemUsed, HandleActiveItemUsed);
            EventManager.Subscribe(EventName.OnItemRemoved, HandleItemRemoved);
            SubscribeSlotClicks();
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleItemObtained);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleActiveItemUsed);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleItemRemoved);
            UnsubscribeSlotClicks();
            _bound = false;
        }

        private void OnEnable()
        {
            Subscribe();
            FetchInitialState();
        }

        private void SubscribeSlotClicks()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var slot = _bindings[i].Slot;
                if (slot != null) slot.OnClicked += HandleSlotClicked;
            }
        }

        private void UnsubscribeSlotClicks()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var slot = _bindings[i].Slot;
                if (slot != null) slot.OnClicked -= HandleSlotClicked;
            }
        }

        private void HandleSlotClicked(ActiveItemSlotView clicked)
        {
            // En combate los ítems activos no se usan vía click — la lógica de combate
            // (ej. botón de Heal) los consume desde la cadena de efectos del behavior.
            if (ServiceLocator.TryGetService<IPhaseService>(out var phase)
                && phase != null
                && phase.CurrentBase != GamePhase.Exploration)
            {
                return;
            }

            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null)
            {
                Debug.LogWarning(LogPrefix + "IInventoryService no registrado — no se puede activar el ítem.");
                return;
            }

            string clickedItemId = ResolveItemIdForSlot(clicked);
            if (string.IsNullOrEmpty(clickedItemId)) return;

            int slotIndex = FindActiveSlotIndex(inventory, clickedItemId);
            if (slotIndex < 0)
            {
                Debug.LogWarning(LogPrefix + $"No hay slot activo para ItemId='{clickedItemId}' en el inventario.");
                return;
            }

            var ctx = BuildSelfTargetedContext();
            inventory.ActivateItem(slotIndex, ctx);
        }

        private string ResolveItemIdForSlot(ActiveItemSlotView slot)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].Slot == slot) return _bindings[i].ItemId;
            }
            return null;
        }

        private static int FindActiveSlotIndex(IInventoryService inventory, string itemId)
        {
            var actives = inventory.ActiveItems;
            for (int i = 0; i < actives.Count; i++)
            {
                var slot = actives[i];
                if (slot?.Item != null
                    && string.Equals(slot.Item.ItemId, itemId, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private EffectContext BuildSelfTargetedContext()
        {
            var ctx = new EffectContext
            {
                SourceGuid = _playerGuid,
                TargetGuid = _playerGuid,
                lastResult = true,
            };

            // Apunta el SelectionResult al tile del player para que efectos como
            // EffHeal (que resuelven target via SelectionResult.FirstSelectedCoord +
            // IGridManager.TryGetOccupant) lo encuentren self-target.
            if (ServiceLocator.TryGetService<IGridManager>(out var grid)
                && grid != null
                && grid.TryGetPosition(_playerGuid, out var coord))
            {
                ctx.SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(coord) },
                };
            }

            return ctx;
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void HandleItemObtained(params object[] args)
        {
            if (!TryReadGuidAndItemId(args, out var guid, out var itemId)) return;
            if (guid != _playerGuid) return;

            if (TryFindSlot(itemId, out var slot))
            {
                slot.SetState(ActiveItemState.Active);
                slot.SetCount(CountInInventory(itemId));
            }
            // else: item legitimo no-active / sin slot HUD — se ignora sin warning.
        }

        private void HandleActiveItemUsed(params object[] args)
        {
            if (!TryReadGuidAndItemId(args, out var guid, out var itemId)) return;
            if (guid != _playerGuid) return;

            if (TryFindSlot(itemId, out var slot))
            {
                int remaining = CountInInventory(itemId);
                if (remaining <= 0)
                {
                    slot.SetState(ActiveItemState.Depleted);
                }
                else
                {
                    slot.SetState(ActiveItemState.Active);
                }
                slot.SetCount(remaining);
            }
        }

        private void HandleItemRemoved(params object[] args)
        {
            if (!TryReadGuidAndItemId(args, out var guid, out var itemId)) return;
            if (guid != _playerGuid) return;

            if (TryFindSlot(itemId, out var slot))
            {
                int remaining = CountInInventory(itemId);
                slot.SetState(remaining > 0 ? ActiveItemState.Active : ActiveItemState.Inactive);
                slot.SetCount(remaining);
            }
        }

        private static int CountInInventory(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inv) || inv == null) return 0;

            int count = 0;
            foreach (var slot in inv.ActiveItems)
            {
                if (slot?.Item != null && slot.Item.ItemId == itemId) count++;
            }
            foreach (var slot in inv.PassiveItems)
            {
                if (slot?.Item != null && slot.Item.ItemId == itemId) count++;
            }
            return count;
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
        /// Lee el inventario actual y refresca cada slot con su estado y count.
        /// Cubre el caso del HUD bindeado tras AddItem (ej. starting items entregados
        /// por <c>RunController.GrantStartingItems</c>).
        /// </summary>
        private void FetchInitialState()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var binding = _bindings[i];
                if (binding.Slot == null) continue;

                int count = CountInInventory(binding.ItemId);
                binding.Slot.SetState(count > 0 ? ActiveItemState.Active : ActiveItemState.Inactive);
                binding.Slot.SetCount(count);
            }
        }
    }
}
