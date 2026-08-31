using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Items;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// La barra de items activos del HUD. Tiene dos poblaciones:
    /// <list type="bullet">
    /// <item><b>Slots pinneados</b> (<see cref="_bindings"/>): posicionados y decorados a
    /// mano en el prefab para un <c>ItemId</c> fijo — hoy la poción, que ademas es
    /// display-only porque se consume via el boton Heal y no por click.</item>
    /// <item><b>Slots dinamicos</b>: uno por cada item activo del inventario que
    /// <i>no</i> tenga slot pinneado. Se instancian desde <see cref="_dynamicSlotPrefab"/>
    /// dentro de <see cref="_dynamicContainer"/> y son clickeables — son la unica forma
    /// de usar un item activo en el juego.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Plan §4.6. El click pasa por <see cref="IInventoryService.CanActivateItem"/> antes
    /// de activar: si esta bloqueado no se ejecuta nada y se muestra el motivo con
    /// <see cref="ActionRejectToast"/>, mismo contrato que los chips de accion.
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
        [InfoBox("Slots pinneados: posicion y sprites a mano para un ItemId fijo. Todo " +
                 "item activo que NO este en esta lista cae en la barra dinamica.")]
        [SerializeField]
        private List<ItemSlotBinding> _bindings = new List<ItemSlotBinding>();

        [Title("Active Items — Barra dinamica")]
        [InfoBox("Contenedor (con HorizontalLayoutGroup) donde se instancian los slots de " +
                 "los items activos que no tienen binding pinneado. Sin contenedor o sin " +
                 "prefab la barra queda vacia y solo funcionan los pinneados.")]
        [SerializeField]
        private RectTransform _dynamicContainer;

        [SerializeField]
        [Tooltip("Prefab del slot generico que se clona por item activo.")]
        private ActiveItemSlotView _dynamicSlotPrefab;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        // BUG-074: el rojo de "sin rolls" solo aplica durante el turno del jugador —
        // espejo del flag de PlayerActionButtonsView, para que la ficha de poción
        // responda al mismo tiempo que los chips de acción.
        private bool _isPlayerTurn;

        /// <summary>Pool reusable de slots dinamicos, en orden de creacion.</summary>
        private readonly List<ActiveItemSlotView> _dynamicSlots = new List<ActiveItemSlotView>();

        /// <summary>
        /// ItemId que representa cada slot dinamico <i>visible</i>, en paralelo a los
        /// primeros N de <see cref="_dynamicSlots"/>. El resto del pool esta apagado.
        /// </summary>
        private readonly List<string> _dynamicItemIds = new List<string>();

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
            // BUG-074: la affordability del slot sigue al pool de rolls y al turno,
            // igual que los chips de PlayerActionButtonsView.
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, HandleRollsOrPhaseChanged);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnPhaseEnter, HandleRollsOrPhaseChanged);
            SubscribeSlotClicks();
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleItemObtained);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleActiveItemUsed);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleItemRemoved);
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, HandleRollsOrPhaseChanged);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnPhaseEnter, HandleRollsOrPhaseChanged);
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
            // Los dinamicos se enganchan al crearse (EnsureDynamicSlots) y viven tanto
            // como la vista, asi que aca solo cubrimos un re-Subscribe con el pool ya
            // poblado — sin el, un OnDisable/OnEnable los dejaba mudos.
            for (int i = 0; i < _dynamicSlots.Count; i++)
            {
                var slot = _dynamicSlots[i];
                if (slot == null) continue;
                slot.OnClicked -= HandleSlotClicked;
                slot.OnClicked += HandleSlotClicked;
            }
        }

        private void UnsubscribeSlotClicks()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                var slot = _bindings[i].Slot;
                if (slot != null) slot.OnClicked -= HandleSlotClicked;
            }
            for (int i = 0; i < _dynamicSlots.Count; i++)
            {
                if (_dynamicSlots[i] != null) _dynamicSlots[i].OnClicked -= HandleSlotClicked;
            }
        }

        // ==================================================================
        // Click → activacion
        // ==================================================================

        private void HandleSlotClicked(ActiveItemSlotView clicked)
        {
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inventory) || inventory == null)
            {
                Debug.LogWarning(LogPrefix + "IInventoryService no registrado — no se puede activar el ítem.");
                return;
            }

            string clickedItemId = ResolveItemIdForSlot(clicked);
            if (string.IsNullOrEmpty(clickedItemId)) return;

            // En combate un item usado fuera del turno propio igual le cobraria un roll
            // al pool del jugador (TurnManager no mira de quien es el turno), asi que el
            // gate de turno va antes que cualquier otra cosa.
            if (IsCombatActive() && !_isPlayerTurn)
            {
                ShowReject(clicked, LocalizedContent.Ui(UiTextKeys.RejectNotYourTurn, "No es tu turno."));
                return;
            }

            int slotIndex = FindActiveSlotIndex(inventory, clickedItemId);
            if (slotIndex < 0)
            {
                ShowReject(clicked, LocalizedContent.Ui(UiTextKeys.RejectUsed, "Ya no te queda."));
                return;
            }

            var ctx = BuildSelfTargetedContext();

            var block = inventory.CanActivateItem(slotIndex, ctx);
            if (block != ItemActivationBlock.None)
            {
                ShowReject(clicked, DescribeBlock(block));
                return;
            }

            inventory.ActivateItem(slotIndex, ctx);
        }

        /// <summary>Motivo localizado del rechazo, para el toast.</summary>
        private static string DescribeBlock(ItemActivationBlock block)
        {
            switch (block)
            {
                case ItemActivationBlock.OnCooldown:
                    return LocalizedContent.Ui(UiTextKeys.RejectOnCooldown, "Todavía en enfriamiento.");
                case ItemActivationBlock.NotEnoughRolls:
                    return LocalizedContent.Ui(UiTextKeys.RejectNoRolls, "No te alcanzan los rolls.");
                case ItemActivationBlock.InvalidSlot:
                case ItemActivationBlock.NotActiveItem:
                    return LocalizedContent.Ui(UiTextKeys.RejectUsed, "Ya no te queda.");
                default:
                    return LocalizedContent.Ui(UiTextKeys.RejectItemUnavailable,
                                               "No podés usar este objeto ahora.");
            }
        }

        /// <summary>
        /// Toast sobre el slot, mismo formato que <c>ExplorationActionButtonsView</c>:
        /// título genérico + motivo concreto.
        /// </summary>
        private static void ShowReject(ActiveItemSlotView slot, string reason)
        {
            if (slot == null || string.IsNullOrEmpty(reason)) return;

            string title = LocalizedContent.Ui(UiTextKeys.RejectTitle,
                "Esta acción no puede ser realizada");
            var label = slot.GetComponentInChildren<TMP_Text>(true);
            ActionRejectToast.Show(slot.transform as RectTransform,
                title + "\n" + reason, label != null ? label.font : null);
        }

        private static bool IsCombatActive()
        {
            return ServiceLocator.TryGetService<Rollgeon.Combat.Rolls.IRollPoolService>(out var rolls)
                   && rolls != null
                   && rolls.IsCombatActive;
        }

        private string ResolveItemIdForSlot(ActiveItemSlotView slot)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].Slot == slot) return _bindings[i].ItemId;
            }
            int dynamicIndex = _dynamicSlots.IndexOf(slot);
            if (dynamicIndex >= 0 && dynamicIndex < _dynamicItemIds.Count)
            {
                return _dynamicItemIds[dynamicIndex];
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
            else
            {
                // Item activo sin slot pinneado → entra a la barra dinamica. Un pasivo
                // simplemente no aparece en ActiveItems y el rebuild lo ignora.
                RebuildDynamicSlots();
            }
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
            else
            {
                RebuildDynamicSlots();
            }

            // El uso puede haber prendido un cooldown o vaciado el pool: repintar todo.
            RefreshAffordability();
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
            else
            {
                RebuildDynamicSlots();
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

            RebuildDynamicSlots();
        }

        // ==================================================================
        // Barra dinamica
        // ==================================================================

        /// <summary>
        /// Repuebla la barra con un slot por cada <c>ItemId</c> activo del inventario que
        /// no tenga binding pinneado, agrupando las cargas repetidas en un solo slot con
        /// contador. El pool se reusa y solo se apaga — mismo criterio que
        /// <c>InventoryDrawerView.EnsureSlots</c>.
        /// </summary>
        public void RebuildDynamicSlots()
        {
            if (_dynamicContainer == null || _dynamicSlotPrefab == null) return;

            _dynamicItemIds.Clear();
            var counts = new List<int>();
            var icons = new List<Sprite>();

            if (ServiceLocator.TryGetService<IInventoryService>(out var inv) && inv != null)
            {
                var actives = inv.ActiveItems;
                for (int i = 0; i < actives.Count; i++)
                {
                    var item = actives[i]?.Item;
                    if (item == null || string.IsNullOrEmpty(item.ItemId)) continue;
                    if (IsPinned(item.ItemId)) continue;

                    int existing = _dynamicItemIds.IndexOf(item.ItemId);
                    if (existing >= 0)
                    {
                        counts[existing]++;
                    }
                    else
                    {
                        _dynamicItemIds.Add(item.ItemId);
                        counts.Add(1);
                        icons.Add(item.Icon);
                    }
                }
            }

            EnsureDynamicSlots(_dynamicItemIds.Count);

            for (int i = 0; i < _dynamicSlots.Count; i++)
            {
                var slot = _dynamicSlots[i];
                if (slot == null) continue;

                bool used = i < _dynamicItemIds.Count;
                slot.gameObject.SetActive(used);
                if (!used) continue;

                slot.SetState(ActiveItemState.Active);
                slot.SetIcon(icons[i]);
                slot.SetCount(counts[i]);
            }

            RefreshAffordability();
        }

        private bool IsPinned(string itemId)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (string.Equals(_bindings[i].ItemId, itemId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private void EnsureDynamicSlots(int needed)
        {
            while (_dynamicSlots.Count < needed)
            {
                var slot = Instantiate(_dynamicSlotPrefab, _dynamicContainer);
                slot.OnClicked += HandleSlotClicked;
                _dynamicSlots.Add(slot);
            }
        }

        // ==================================================================
        // BUG-074 — affordability por pool de rolls
        // ==================================================================

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            _isPlayerTurn = guid == _playerGuid;
            RefreshAffordability();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _isPlayerTurn = false;
            RefreshAffordability();
        }

        private void HandleRollsOrPhaseChanged(params object[] args) => RefreshAffordability();

        /// <summary>
        /// Pinta de rojo lo que no se puede usar ahora. Los pinneados siguen la regla
        /// vieja (pool de rolls durante el turno propio); los dinamicos preguntan el
        /// motivo real a <see cref="IInventoryService.CanActivateItem"/>, asi el rojo
        /// coincide exactamente con lo que el click va a rechazar.
        /// </summary>
        private void RefreshAffordability()
        {
            bool combat = IsCombatActive();
            bool pinnedAffordable = true;
            if (_isPlayerTurn
                && ServiceLocator.TryGetService<Rollgeon.Combat.Rolls.IRollPoolService>(out var rolls)
                && rolls != null
                && rolls.IsCombatActive)
            {
                pinnedAffordable = rolls.GetCurrent(_playerGuid) >= 1;
            }

            for (int i = 0; i < _bindings.Count; i++)
            {
                _bindings[i].Slot?.SetAffordable(pinnedAffordable);
            }

            if (_dynamicItemIds.Count == 0) return;

            ServiceLocator.TryGetService<IInventoryService>(out var inv);
            // Sin inventario no hay nada que evaluar; el rebuild ya dejo la barra vacia.
            if (inv == null) return;

            var ctx = BuildSelfTargetedContext();
            for (int i = 0; i < _dynamicItemIds.Count && i < _dynamicSlots.Count; i++)
            {
                var slot = _dynamicSlots[i];
                if (slot == null) continue;

                bool usable = !combat || _isPlayerTurn;
                if (usable)
                {
                    int index = FindActiveSlotIndex(inv, _dynamicItemIds[i]);
                    usable = index >= 0 && inv.CanActivateItem(index, ctx) == ItemActivationBlock.None;
                }
                slot.SetAffordable(usable);
            }
        }
    }
}
