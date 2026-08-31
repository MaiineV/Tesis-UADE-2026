using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Patterns.Save;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Rolls;
using Rollgeon.Combos.Play;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Player;
using Rollgeon.PreConditions;
using Rollgeon.Upgrades;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Items
{
    public sealed class InventoryService : IInventoryService, ISaveable, IDisposable
    {
        private readonly List<InventorySlot> _passiveItems = new();
        private readonly List<InventorySlot> _activeItems = new();

        /// <summary>
        /// Cada handler suscripto, con el item que lo puso. El <c>itemId</c> es lo que hace posible
        /// desenganchar un item sin tocar los de otro — ver <see cref="UnbindPassiveHooks"/>.
        /// Misma clave que <see cref="_appliedModifierIds"/>, así los dos tratan igual a un item.
        /// </summary>
        private readonly List<(string itemId, EventName evt, EventManager.EventReceiver handler)> _hookHandlers = new();

        /// <summary>
        /// Handlers tipados de ComboPlayed, en lista paralela a <see cref="_hookHandlers"/>
        /// (el bus tipado no comparte el shape de EventReceiver). Mismo contrato de unbind
        /// por <c>itemId</c>.
        /// </summary>
        private readonly List<(string itemId, Action<ComboPlayedPayload> handler)> _comboPlayedHandlers = new();
        private readonly Dictionary<string, List<Guid>> _appliedModifierIds = new();

        private readonly ItemCatalogSO _catalog;
        private readonly int _maxActiveSlots;

        public IReadOnlyList<InventorySlot> PassiveItems => _passiveItems;
        public IReadOnlyList<InventorySlot> ActiveItems => _activeItems;
        public int MaxActiveSlots => _maxActiveSlots;

        public event Action<ItemSO, bool> OnItemChanged;

        /// <summary>
        /// Handler de <see cref="EventName.OnTurnFinished"/> que baja los cooldowns.
        /// Guardado en campo para poder desuscribir en <see cref="Dispose"/>.
        /// </summary>
        private readonly EventManager.EventReceiver _onTurnFinishedHandler;

        public InventoryService(ItemCatalogSO catalog, int maxActiveSlots)
        {
            _catalog = catalog;
            _maxActiveSlots = Mathf.Max(1, maxActiveSlots);

            // Sin esto TickCooldowns no lo llamaba nadie y un item con Cooldown > 0
            // quedaba bloqueado para el resto de la run. Mismo patron de hook por turno
            // que ComboBlockService / DiceBlockService.
            _onTurnFinishedHandler = HandleTurnFinished;
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
        }

        /// <summary>
        /// Solo el turno del jugador descuenta: el cooldown de un item se mide en turnos
        /// propios, no en turnos de mesa.
        /// </summary>
        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != GetPlayerGuid()) return;
            TickCooldowns();
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

        /// <summary>
        /// Espejo read-only de los gates de <see cref="ActivateItem"/>. Es la unica
        /// fuente de verdad: <c>ActivateItem</c> lo llama primero, asi el rojo del HUD y
        /// el rechazo real no pueden divergir.
        /// </summary>
        public ItemActivationBlock CanActivateItem(int activeSlotIndex, EffectContext ctx)
        {
            if (activeSlotIndex < 0 || activeSlotIndex >= _activeItems.Count)
                return ItemActivationBlock.InvalidSlot;

            var slot = _activeItems[activeSlotIndex];
            if (slot.CurrentCooldown > 0) return ItemActivationBlock.OnCooldown;

            var item = slot.Item;
            if (item == null || item.Type != ItemType.Active)
                return ItemActivationBlock.NotActiveItem;

            var playerGuid = ctx?.SourceGuid ?? GetPlayerGuid();

            if (item.ConsumesAction)
            {
                if (!ServiceLocator.TryGetService<TurnManager>(out var tm) || tm == null)
                    return ItemActivationBlock.ForbiddenByRuleset;

                if (!tm.CanExecute(BuildUseItemAction(item), playerGuid, out _))
                {
                    // TurnManager devuelve el motivo como string en ingles; en vez de
                    // parsearlo, re-preguntamos al pool para separar "no te alcanza" de
                    // "el ruleset lo prohibe".
                    bool poolEmpty = ServiceLocator.TryGetService<IRollPoolService>(out var rolls)
                                     && rolls != null
                                     && rolls.IsCombatActive
                                     && rolls.GetCurrent(playerGuid) < 1;
                    return poolEmpty
                        ? ItemActivationBlock.NotEnoughRolls
                        : ItemActivationBlock.ForbiddenByRuleset;
                }
            }

            // Las precondiciones del OnActivate valen para las dos ramas. Antes la rama
            // ConsumesAction solo las veia dentro de TurnManager.TryExecute — o sea,
            // despues de cobrar el roll.
            if (item.OnActivate != null && !item.OnActivate.CanBeExecuted(BuildPreCtx(ctx)))
                return ItemActivationBlock.PreconditionFailed;

            return ItemActivationBlock.None;
        }

        private static ActionDefinitionSO BuildUseItemAction(ItemSO item)
        {
            return new ActionDefinitionSO
            {
                ActionId = item.ResolvedActionId,
                Type = ActionType.UseItem,
                BackingAsset = item,
                Effect = item.OnActivate,
            };
        }

        public bool ActivateItem(int activeSlotIndex, EffectContext ctx)
        {
            var block = CanActivateItem(activeSlotIndex, ctx);
            if (block != ItemActivationBlock.None)
            {
                if (block == ItemActivationBlock.ForbiddenByRuleset
                    && !ServiceLocator.TryGetService<TurnManager>(out _))
                {
                    Debug.LogWarning("[InventoryService] TurnManager not registered — cannot enforce action economy.");
                }
                return false;
            }

            var slot = _activeItems[activeSlotIndex];
            var item = slot.Item;

            if (item.ConsumesAction)
            {
                var playerGuid = ctx?.SourceGuid ?? GetPlayerGuid();
                ServiceLocator.TryGetService<TurnManager>(out var tm);
                if (!tm.TryExecute(BuildUseItemAction(item), playerGuid, ctx)) return false;
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
        // Preview helpers
        // ======================================================================

        /// <summary>
        /// Suma el bono de daño at-played (<see cref="EffAddComboBonus"/>) que los items
        /// passive del inventario aportarían al <paramref name="comboId"/>. Lo usa el
        /// preview de daño para mostrar la contribución de los objetos ANTES de jugar el
        /// combo — el bono real se aplica recién en ComboPlayed (ver <c>LastPlayScratch</c>).
        /// Solo suma <see cref="EffAddComboBonus"/> (evita side-effects de otros efectos del
        /// hook); readers dinámicos se leen con un contexto mínimo.
        /// </summary>
        public int GetComboDamageBonusPreview(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return 0;

            var ctx = new EffectContext { SourceGuid = GetPlayerGuid() };
            int total = 0;
            foreach (var slot in _passiveItems)
            {
                var item = slot?.Item;
                if (item?.PassiveHooks == null) continue;
                foreach (var hook in item.PassiveHooks)
                {
                    if (hook == null || hook.Kind != PassiveHookKind.ComboPlayed) continue;
                    if (hook.ComboFilter != null && !hook.ComboFilter.Matches(comboId)) continue;
                    if (hook.Effect?.Effects == null) continue;
                    foreach (var eff in hook.Effect.Effects)
                    {
                        if (eff is EffAddComboBonus bonus && bonus.Amount != null)
                            total += bonus.Amount.Read(ctx);
                    }
                }
            }
            return total;
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

                if (hook.Kind == PassiveHookKind.ComboPlayed)
                {
                    BindComboPlayedHook(item, hook);
                    continue;
                }

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
                _hookHandlers.Add((item.ItemId, hook.TriggerEvent, handler));
            }
        }

        private void BindComboPlayedHook(ItemSO item, PassiveItemHook hook)
        {
            var capturedHook = hook;
            var capturedItem = item;
            Action<ComboPlayedPayload> handler = payload =>
            {
                var playerGuid = GetPlayerGuid();
                if (payload.SourceGuid != playerGuid) return;
                if (capturedHook.ComboFilter != null && !capturedHook.ComboFilter.Matches(payload.ComboId)) return;
                // BUG-080: opt-in por hook — un bono de daño (El Egoísta) no debe leakear a
                // Heal/Movement, que comparten el mismo play scratch que PlayerComboDamage/
                // PlayerComboHeal leen. Unknown (default) = sin restricción, preserva items
                // existentes que reaccionan a cualquier ComboPlayed.
                if (capturedHook.ActionKindFilter != Rollgeon.Combat.Rolls.RollActionKind.Unknown
                    && payload.ActionKind != capturedHook.ActionKindFilter) return;

                // El efecto corre DENTRO de la ventana de combo jugado: el play scratch
                // viaja como trigger context para que un EffAddComboBonus del item sume
                // al daño del golpe en curso. Efectos directos (oro, heal) no lo necesitan.
                var play = ServiceLocator.TryGetService<IComboPlayService>(out var p) ? p : null;
                var ctx = new EffectContext
                {
                    SourceGuid = playerGuid,
                    TargetGuid = payload.TargetGuid != Guid.Empty ? payload.TargetGuid : playerGuid,
                    DiceResult = payload.DiceResult,
                    KeptDice = payload.KeptDice,
                    KeptDiceOriginalIndices = payload.KeptDiceOriginalIndices,
                    ComboResult = payload.ComboResult,
                    lastResult = true,
                    TriggerContext = new ScratchTriggerContext
                    {
                        Scratch = play?.CurrentPlayScratch,
                        ComboId = payload.ComboId,
                        Channel = ScratchChannel.Item,
                    },
                };
                var preCtx = new PreConditionContext
                {
                    OwnerGuid = playerGuid,
                    OpponentGuid = ctx.TargetGuid,
                    Effect = ctx,
                };
                // Snapshot-delta por (item, hook): atribuye al journal lo que ESTE item
                // aportó al combo en curso. Sin ventana de play no hay scratch que medir.
                var scratch = play?.CurrentPlayScratch;
                var before = scratch != null ? ScratchSnapshot.Of(scratch) : default;
                capturedHook.Effect.TryExecute(ctx, preCtx);
                if (scratch != null)
                    ScratchSnapshot.RecordDelta(scratch, in before,
                        ScratchSourceKind.Item, capturedItem.ItemId, capturedItem, bagSlot: -1);
            };

            TypedEvent<ComboPlayedPayload>.Subscribe(handler);
            _comboPlayedHandlers.Add((item.ItemId, handler));
        }

        /// <summary>
        /// Desengancha exactamente los handlers que puso <paramref name="item"/>.
        /// </summary>
        /// <remarks>
        /// Matchea por <c>itemId</c>, no por <c>TriggerEvent</c>. Con dos pasivas colgadas del mismo
        /// evento, matchear por evento desenganchaba una cualquiera (la última, por el recorrido
        /// LIFO): quitar la pasiva A mataba el hook de B y dejaba vivo el de A. Con un solo item
        /// autorado no se notaba.
        /// <para>
        /// Tampoco recorre <c>item.PassiveHooks</c> para decidir qué sacar: se desengancha lo que se
        /// enganchó. Si el SO cambió entre el bind y el unbind, mirar los hooks de ahora dejaría
        /// suscripciones colgadas apuntando a un item que ya no está en el inventario.
        /// </para>
        /// </remarks>
        private void UnbindPassiveHooks(ItemSO item)
        {
            if (item == null || string.IsNullOrEmpty(item.ItemId)) return;

            for (int i = _hookHandlers.Count - 1; i >= 0; i--)
            {
                if (_hookHandlers[i].itemId != item.ItemId) continue;

                EventManager.UnSubscribe(_hookHandlers[i].evt, _hookHandlers[i].handler);
                _hookHandlers.RemoveAt(i);
            }

            for (int i = _comboPlayedHandlers.Count - 1; i >= 0; i--)
            {
                if (_comboPlayedHandlers[i].itemId != item.ItemId) continue;

                TypedEvent<ComboPlayedPayload>.Unsubscribe(_comboPlayedHandlers[i].handler);
                _comboPlayedHandlers.RemoveAt(i);
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
        // ISaveable (§15) — el service vivo es lo que se registra; InventoryState
        // queda como converter/DTO holder para los helpers de arriba y sus tests.
        // ======================================================================

        string ISaveable.SaveKey => "run.inventory";

        object ISaveable.CaptureState() => CaptureState().CaptureState();

        void ISaveable.RestoreState(object state)
        {
            var holder = new InventoryState();
            holder.RestoreState(state);
            RestoreState(holder);
        }

        // ======================================================================
        // Dispose
        // ======================================================================

        public void Dispose()
        {
            SaveSystem.Unregister(this);
            EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            ClearAllHooksAndModifiers();
            _passiveItems.Clear();
            _activeItems.Clear();
        }

        private void ClearAllHooksAndModifiers()
        {
            foreach (var (_, evt, handler) in _hookHandlers)
                EventManager.UnSubscribe(evt, handler);
            _hookHandlers.Clear();

            foreach (var (_, handler) in _comboPlayedHandlers)
                TypedEvent<ComboPlayedPayload>.Unsubscribe(handler);
            _comboPlayedHandlers.Clear();

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
