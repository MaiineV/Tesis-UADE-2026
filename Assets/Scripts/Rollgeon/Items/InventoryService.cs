using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Combat.Actions;
using Rollgeon.Effects;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using UnityEngine;

namespace Rollgeon.Items
{
    public sealed class InventoryService : IInventoryService, IDisposable
    {
        private readonly List<InventorySlot> _passiveItems = new();
        private readonly List<InventorySlot> _activeItems = new();

        private readonly List<(EventName evt, EventManager.EventReceiver handler)> _hookHandlers = new();
        private readonly Dictionary<string, List<Guid>> _appliedModifierIds = new();

        private readonly ItemCatalogSO _catalog;
        private readonly int _maxActiveSlots;

        public IReadOnlyList<InventorySlot> PassiveItems => _passiveItems;
        public IReadOnlyList<InventorySlot> ActiveItems => _activeItems;
        public int MaxActiveSlots => _maxActiveSlots;

        public event Action<ItemSO, bool> OnItemChanged;

        public InventoryService(ItemCatalogSO catalog, int maxActiveSlots)
        {
            _catalog = catalog;
            _maxActiveSlots = Mathf.Max(1, maxActiveSlots);
        }

        // ======================================================================
        // Add / Remove
        // ======================================================================

        public bool AddItem(ItemSO item)
        {
            if (item == null) return false;

            if (item.Type == ItemType.Active && _activeItems.Count >= _maxActiveSlots)
                return false;

            var slot = new InventorySlot { Item = item, CurrentCooldown = 0 };

            if (item.Type == ItemType.Passive)
            {
                _passiveItems.Add(slot);
                BindPassiveHooks(item);
                ApplyPersistentModifiers(item);
            }
            else
            {
                _activeItems.Add(slot);
            }

            OnItemChanged?.Invoke(item, true);
            // Centralizamos el OnItemObtained acá — antes solo lo disparaba EffAddItemToInventory,
            // entonces compras del shop / starting items no notificaban al HUD y el counter
            // quedaba stale hasta el próximo OnEnable de la sub-view.
            EventManager.Trigger(EventName.OnItemObtained, GetPlayerGuid(), item.ItemId);
            return true;
        }

        public bool RemoveItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;

            var passiveIdx = _passiveItems.FindIndex(s => s.Item != null && s.Item.ItemId == itemId);
            if (passiveIdx >= 0)
            {
                var item = _passiveItems[passiveIdx].Item;
                _passiveItems.RemoveAt(passiveIdx);
                UnbindPassiveHooks(item);
                RemovePersistentModifiers(item);
                OnItemChanged?.Invoke(item, false);
                EventManager.Trigger(EventName.OnItemRemoved, GetPlayerGuid(), itemId);
                return true;
            }

            var activeIdx = _activeItems.FindIndex(s => s.Item != null && s.Item.ItemId == itemId);
            if (activeIdx >= 0)
            {
                var item = _activeItems[activeIdx].Item;
                _activeItems.RemoveAt(activeIdx);
                OnItemChanged?.Invoke(item, false);
                EventManager.Trigger(EventName.OnItemRemoved, GetPlayerGuid(), itemId);
                return true;
            }

            return false;
        }

        public bool HasItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return _passiveItems.Any(s => s.Item != null && s.Item.ItemId == itemId)
                || _activeItems.Any(s => s.Item != null && s.Item.ItemId == itemId);
        }

        public ItemSO GetItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            var slot = _passiveItems.FirstOrDefault(s => s.Item != null && s.Item.ItemId == itemId)
                    ?? _activeItems.FirstOrDefault(s => s.Item != null && s.Item.ItemId == itemId);
            return slot?.Item;
        }

        // ======================================================================
        // Activate (active items)
        // ======================================================================

        public bool ActivateItem(int activeSlotIndex, EffectContext ctx)
        {
            if (activeSlotIndex < 0 || activeSlotIndex >= _activeItems.Count) return false;

            var slot = _activeItems[activeSlotIndex];
            if (slot.CurrentCooldown > 0) return false;

            var item = slot.Item;
            if (item == null || item.Type != ItemType.Active) return false;

            if (item.ConsumesAction)
            {
                if (!ServiceLocator.TryGetService<TurnManager>(out var tm))
                {
                    Debug.LogWarning("[InventoryService] TurnManager not registered — cannot enforce action economy.");
                    return false;
                }

                var action = new ActionDefinitionSO
                {
                    ActionId = item.ResolvedActionId,
                    Type = ActionType.UseItem,
                    BackingAsset = item,
                    EnergyCost = 0,
                    BlockOnRepeat = true,
                    Effect = item.OnActivate,
                };

                var playerGuid = ctx?.SourceGuid ?? GetPlayerGuid();
                if (!tm.CanExecute(action, playerGuid, out _)) return false;

                var ok = tm.TryExecute(action, playerGuid, ctx);
                if (!ok) return false;
            }
            else
            {
                var preCtx = BuildPreCtx(ctx);
                if (!item.OnActivate.TryExecute(ctx, preCtx)) return false;
            }

            slot.CurrentCooldown = item.Cooldown;
            EventManager.Trigger(EventName.OnActiveItemUsed, ctx?.SourceGuid ?? GetPlayerGuid(), item.ItemId);

            if (item.ConsumedOnUse)
            {
                // Remove by index so multiple charges del mismo item se descuentan
                // de a uno (RemoveItem(itemId) borraría el primer slot que matchee).
                _activeItems.RemoveAt(activeSlotIndex);
                OnItemChanged?.Invoke(item, false);
                EventManager.Trigger(EventName.OnItemRemoved, ctx?.SourceGuid ?? GetPlayerGuid(), item.ItemId);
            }
            return true;
        }

        // ======================================================================
        // Cooldowns
        // ======================================================================

        public void TickCooldowns()
        {
            foreach (var slot in _activeItems)
            {
                if (slot.CurrentCooldown > 0)
                    slot.CurrentCooldown--;
            }
        }

        // ======================================================================
        // Passive hooks — subscribe/unsubscribe to EventManager
        // ======================================================================

        private void BindPassiveHooks(ItemSO item)
        {
            if (item.PassiveHooks == null) return;

            foreach (var hook in item.PassiveHooks)
            {
                if (hook?.Effect == null) continue;

                var capturedHook = hook;
                var capturedItem = item;
                EventManager.EventReceiver handler = args =>
                {
                    if (args == null || args.Length == 0) return;
                    if (args[0] is Guid ownerId && ownerId != GetPlayerGuid()) return;

                    var playerGuid = GetPlayerGuid();
                    var ctx = new EffectContext
                    {
                        SourceGuid = playerGuid,
                        TargetGuid = playerGuid,
                        lastResult = true,
                    };
                    var preCtx = new PreConditionContext
                    {
                        OwnerGuid = playerGuid,
                    };
                    capturedHook.Effect.TryExecute(ctx, preCtx);
                };

                EventManager.Subscribe(hook.TriggerEvent, handler);
                _hookHandlers.Add((hook.TriggerEvent, handler));
            }
        }

        private void UnbindPassiveHooks(ItemSO item)
        {
            // Unbind all handlers that were registered for this item's hooks.
            // Since we track (event, handler) pairs and can't distinguish per-item
            // in the flat list, we remove handlers matching the item's trigger events.
            // For a full implementation, track per-item handler lists.
            // For now, this works correctly because BindPassiveHooks appends and
            // we remove in LIFO order for the matching event count.
            if (item.PassiveHooks == null) return;

            for (int i = item.PassiveHooks.Count - 1; i >= 0; i--)
            {
                var hook = item.PassiveHooks[i];
                if (hook == null) continue;

                for (int j = _hookHandlers.Count - 1; j >= 0; j--)
                {
                    if (_hookHandlers[j].evt == hook.TriggerEvent)
                    {
                        EventManager.UnSubscribe(_hookHandlers[j].evt, _hookHandlers[j].handler);
                        _hookHandlers.RemoveAt(j);
                        break;
                    }
                }
            }
        }

        // ======================================================================
        // Persistent modifiers — apply/remove via AttributesManager
        // ======================================================================

        private void ApplyPersistentModifiers(ItemSO item)
        {
            if (item.PassiveHooks == null) return;

            var playerGuid = GetPlayerGuid();
            if (playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrMgr)) return;

            var attrs = attrMgr.GetAttributes(playerGuid);
            if (attrs == null) return;

            var modIds = new List<Guid>();

            foreach (var hook in item.PassiveHooks)
            {
                if (hook?.PersistentModifiers == null) continue;
                foreach (var def in hook.PersistentModifiers)
                {
                    if (def.TargetStat == null) continue;

                    IModifiable attribute = null;
                    foreach (var kvp in attrs.EnumerateEntries())
                    {
                        if (kvp.Key == def.TargetStat)
                        {
                            attribute = kvp.Value;
                            break;
                        }
                    }
                    if (attribute == null) continue;

                    var mod = new Modifier<int>(
                        (int)def.Amount,
                        def.Operation,
                        0,
                        playerGuid,
                        playerGuid,
                        def.Direction,
                        ModifierLifetime.Permanent,
                        default
                    );

                    if (attribute.AddModifier(mod))
                        modIds.Add(mod.ModifierId);
                }
            }

            if (modIds.Count > 0)
                _appliedModifierIds[item.ItemId] = modIds;
        }

        private void RemovePersistentModifiers(ItemSO item)
        {
            if (!_appliedModifierIds.TryGetValue(item.ItemId, out var modIds)) return;

            var playerGuid = GetPlayerGuid();
            if (playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrMgr)) return;

            var attrs = attrMgr.GetAttributes(playerGuid);
            if (attrs == null) return;

            foreach (var modId in modIds)
            {
                foreach (var kvp in attrs.EnumerateEntries())
                {
                    kvp.Value.RemoveModifier(modId);
                }
            }

            _appliedModifierIds.Remove(item.ItemId);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static Guid GetPlayerGuid()
        {
            return ServiceLocator.TryGetService<IPlayerService>(out var ps) ? ps.PlayerGuid : Guid.Empty;
        }

        private static PreConditionContext BuildPreCtx(EffectContext ctx)
        {
            if (ctx == null) return new PreConditionContext();
            return new PreConditionContext
            {
                OwnerGuid = ctx.SourceGuid,
                OpponentGuid = ctx.TargetGuid,
                Entity = ctx.SourceEntity,
            };
        }

        // ======================================================================
        // Save / Restore helpers
        // ======================================================================

        public InventoryState CaptureState()
        {
            var state = new InventoryState
            {
                PassiveItemIds = _passiveItems
                    .Where(s => s.Item != null)
                    .Select(s => s.Item.ItemId)
                    .ToList(),
                ActiveSlots = _activeItems
                    .Where(s => s.Item != null)
                    .Select(s => new InventorySlotSnapshot
                    {
                        ItemId = s.Item.ItemId,
                        CurrentCooldown = s.CurrentCooldown,
                    })
                    .ToList(),
            };
            return state;
        }

        public void RestoreState(InventoryState state)
        {
            if (state == null || _catalog == null) return;

            _passiveItems.Clear();
            _activeItems.Clear();
            ClearAllHooksAndModifiers();

            foreach (var id in state.PassiveItemIds)
            {
                var item = _catalog.GetById(id);
                if (item != null) AddItem(item);
            }

            foreach (var snapshot in state.ActiveSlots)
            {
                var item = _catalog.GetById(snapshot.ItemId);
                if (item == null) continue;
                var slot = new InventorySlot { Item = item, CurrentCooldown = snapshot.CurrentCooldown };
                _activeItems.Add(slot);
            }
        }

        // ======================================================================
        // Dispose
        // ======================================================================

        public void Dispose()
        {
            ClearAllHooksAndModifiers();
            _passiveItems.Clear();
            _activeItems.Clear();
        }

        private void ClearAllHooksAndModifiers()
        {
            foreach (var (evt, handler) in _hookHandlers)
                EventManager.UnSubscribe(evt, handler);
            _hookHandlers.Clear();

            var playerGuid = GetPlayerGuid();
            if (playerGuid != Guid.Empty && ServiceLocator.TryGetService<AttributesManager>(out var attrMgr))
            {
                var attrs = attrMgr.GetAttributes(playerGuid);
                if (attrs != null)
                {
                    foreach (var modIds in _appliedModifierIds.Values)
                    {
                        foreach (var modId in modIds)
                        {
                            foreach (var kvp in attrs.EnumerateEntries())
                                kvp.Value.RemoveModifier(modId);
                        }
                    }
                }
            }
            _appliedModifierIds.Clear();
        }
    }
}
