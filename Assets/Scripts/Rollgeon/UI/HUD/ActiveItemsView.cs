using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Exploration;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// La barra de items activos del HUD: <b>un slot por carga</b> del inventario, en el
    /// orden de <see cref="IInventoryService.ActiveItems"/> y hasta
    /// <c>MaxActiveSlots</c>. Dos cargas de la misma poción son dos slots, y el click
    /// consume la que tocaste — el índice del slot es el índice del inventario.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El click pasa por <see cref="IInventoryService.CanActivateItem"/> antes de activar:
    /// si está bloqueado no ejecuta nada y muestra el motivo con
    /// <see cref="ActionRejectToast"/>. El gate de turno y el de <c>ActionId</c> repetido
    /// viven en <c>TurnManager</c> y solo aplican a items con <c>ConsumesAction</c>; los
    /// gratis se pueden usar incluso en el turno enemigo.
    /// </para>
    /// <para>
    /// <b>Targeting:</b> lo define la <c>Selection</c> de cada efecto del
    /// <c>OnActivate</c>. Si algún efecto pide selección, se abre
    /// <see cref="ISelectionController"/> y el item se activa recién con el resultado;
    /// si el jugador cancela, el item no se gasta.
    /// </para>
    /// <para>
    /// <b>Delegación a behaviors:</b> algunos items no se resuelven por su
    /// <c>OnActivate</c> sino por un <see cref="HeroActionBehavior"/> de la clase — la
    /// poción se cura por el slot <c>Healing</c>, el mismo que el botón Heal. Esos items
    /// se declaran en <see cref="_behaviorDelegates"/> y su click dispara el behavior,
    /// así los dos caminos son idénticos por construcción.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Active Items View")]
    public class ActiveItemsView : MonoBehaviour
    {
        private const string LogPrefix = "[ActiveItemsView] ";

        /// <summary>
        /// Declara que un <c>ItemId</c> se usa disparando un behavior de la clase en vez
        /// de su propio <c>OnActivate</c>.
        /// </summary>
        [Serializable]
        public struct BehaviorDelegate
        {
            [Tooltip("Id del item en el catalogo. Ej: 'potion.healing'.")]
            public string ItemId;

            [Tooltip("Slot del HeroActionBehavior que resuelve el uso de este item.")]
            public HeroBehaviorSlot Slot;
        }

        [Title("Active Items — Barra")]
        [InfoBox("Un slot por carga del inventario, hasta MaxActiveSlots. Los slots se " +
                 "instancian desde el prefab dentro del contenedor.")]
        [SerializeField]
        private RectTransform _slotsContainer;

        [SerializeField]
        [Tooltip("Prefab del slot que se clona por carga.")]
        private ActiveItemSlotView _slotPrefab;

        [Title("Delegacion a behaviors de la clase")]
        [InfoBox("Items cuyo uso lo resuelve un behavior (la pocion → Healing) en vez de " +
                 "su OnActivate. Sin entrada, el item se activa por ActivateItem.")]
        [SerializeField]
        private List<BehaviorDelegate> _behaviorDelegates = new List<BehaviorDelegate>();

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        /// <summary>Pool reusable de slots, en orden de creacion.</summary>
        private readonly List<ActiveItemSlotView> _slots = new List<ActiveItemSlotView>();

        /// <summary>
        /// Indice en <c>IInventoryService.ActiveItems</c> de cada slot visible, en
        /// paralelo a los primeros N de <see cref="_slots"/>. Es lo que hace que se gaste
        /// la carga que tocaste y no la primera que matchee por ItemId.
        /// </summary>
        private readonly List<int> _slotInventoryIndex = new List<int>();

        /// <summary>Slot que disparó la selección de objetivo en curso, si hay una.</summary>
        private int _pendingSelectionIndex = -1;

        public void Bind(Guid playerGuid)
        {
            _playerGuid = playerGuid;
            if (!_bound) Subscribe();
            Rebuild();
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
            EventManager.Subscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnPlayerRollsChanged, HandleRefreshOnly);
            EventManager.Subscribe(EventName.OnTurnStarted, HandleRefreshOnly);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleRefreshOnly);
            EventManager.Subscribe(EventName.OnPhaseEnter, HandleRefreshOnly);
            _bound = true;
        }

        private void Unsubscribe()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnPlayerRollsChanged, HandleRefreshOnly);
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleRefreshOnly);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleRefreshOnly);
            EventManager.UnSubscribe(EventName.OnPhaseEnter, HandleRefreshOnly);
            _bound = false;
        }

        private void OnEnable()
        {
            Subscribe();
            Rebuild();
        }

        private void OnDisable()
        {
            CancelPendingSelection();
            Unsubscribe();
        }

        private void HandleInventoryChanged(params object[] args)
        {
            // Los eventos de item traen (Guid, itemId); solo nos interesa el del player.
            if (args != null && args.Length >= 1 && args[0] is Guid guid && guid != _playerGuid) return;
            Rebuild();
        }

        private void HandleRefreshOnly(params object[] args) => RefreshSlotStates();

        // ==================================================================
        // Poblado — un slot por carga
        // ==================================================================

        /// <summary>
        /// Repuebla la barra con una entrada por carga del inventario. El pool se reusa y
        /// solo se apaga — mismo criterio que <c>InventoryDrawerView.EnsureSlots</c>.
        /// </summary>
        public void Rebuild()
        {
            if (_slotsContainer == null || _slotPrefab == null) return;

            _slotInventoryIndex.Clear();
            var icons = new List<Sprite>();

            if (ServiceLocator.TryGetService<IInventoryService>(out var inv) && inv != null)
            {
                var actives = inv.ActiveItems;
                int cap = Mathf.Min(actives.Count, Mathf.Max(1, inv.MaxActiveSlots));
                for (int i = 0; i < cap; i++)
                {
                    var item = actives[i]?.Item;
                    if (item == null) continue;
                    _slotInventoryIndex.Add(i);
                    icons.Add(item.Icon);
                }
            }

            EnsureSlots(_slotInventoryIndex.Count);

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                bool used = i < _slotInventoryIndex.Count;
                slot.gameObject.SetActive(used);
                if (!used) continue;

                slot.SetState(ActiveItemState.Active);
                slot.SetIcon(icons[i]);
                // Una carga = un slot: el contador de cantidad ya no aplica.
                slot.SetCount(0);
            }

            RefreshSlotStates();
        }

        private void EnsureSlots(int needed)
        {
            while (_slots.Count < needed)
            {
                var slot = Instantiate(_slotPrefab, _slotsContainer);
                slot.OnClicked += HandleSlotClicked;
                _slots.Add(slot);
            }
        }

        /// <summary>
        /// Repinta cooldown y disponibilidad de cada slot visible sin repoblar. Se llama
        /// en cada cambio de turno, de rolls o de fase — el cooldown baja al cerrar el
        /// turno del jugador, así que no hace falta un evento propio.
        /// </summary>
        private void RefreshSlotStates()
        {
            if (_slotInventoryIndex.Count == 0) return;
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inv) || inv == null) return;

            var actives = inv.ActiveItems;
            var ctx = BuildContext(null);

            for (int i = 0; i < _slotInventoryIndex.Count && i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                int index = _slotInventoryIndex[i];
                if (index < 0 || index >= actives.Count) continue;

                slot.SetCooldown(actives[index].CurrentCooldown);
                slot.SetAffordable(ResolveBlock(inv, index, ctx) == ItemActivationBlock.None);
            }
        }

        /// <summary>
        /// Motivo por el que un slot no se puede usar. Los items delegados en un behavior
        /// no pasan por <c>CanActivateItem</c> — su gating lo decide el behavior, igual
        /// que en el chip de acción correspondiente.
        /// </summary>
        private ItemActivationBlock ResolveBlock(IInventoryService inv, int index, EffectContext ctx)
        {
            var item = inv.ActiveItems[index]?.Item;
            if (item != null && TryGetBehaviorSlot(item.ItemId, out _)) return ItemActivationBlock.None;
            return inv.CanActivateItem(index, ctx);
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

            int slotIndex = ResolveInventoryIndex(clicked);
            if (slotIndex < 0 || slotIndex >= inventory.ActiveItems.Count) return;

            var item = inventory.ActiveItems[slotIndex]?.Item;
            if (item == null) return;

            // Items mediados por un behavior de la clase (la poción): disparar el chip es
            // literalmente lo mismo que apretar el botón, sin duplicar gating ni balance.
            if (TryGetBehaviorSlot(item.ItemId, out var behaviorSlot))
            {
                if (!TriggerBehavior(behaviorSlot))
                {
                    ShowReject(clicked, LocalizedContent.Ui(UiTextKeys.RejectItemUnavailable,
                                                            "No podés usar este objeto ahora."));
                }
                return;
            }

            var block = inventory.CanActivateItem(slotIndex, BuildContext(null));
            if (block != ItemActivationBlock.None)
            {
                ShowReject(clicked, DescribeBlock(block));
                return;
            }

            // El targeting lo define la Selection de los efectos: si alguno pide elegir,
            // se abre el selector y la activación espera al resultado.
            if (TryBeginTargetSelection(item, slotIndex)) return;

            inventory.ActivateItem(slotIndex, BuildContext(null));
        }

        private int ResolveInventoryIndex(ActiveItemSlotView slot)
        {
            int visualIndex = _slots.IndexOf(slot);
            if (visualIndex < 0 || visualIndex >= _slotInventoryIndex.Count) return -1;
            return _slotInventoryIndex[visualIndex];
        }

        private bool TryGetBehaviorSlot(string itemId, out HeroBehaviorSlot slot)
        {
            slot = default;
            if (string.IsNullOrEmpty(itemId)) return false;
            for (int i = 0; i < _behaviorDelegates.Count; i++)
            {
                if (string.Equals(_behaviorDelegates[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    slot = _behaviorDelegates[i].Slot;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Dispara el behavior por el mismo camino que el chip de acción: en combate via
        /// <see cref="PlayerActionButtonsView"/>, en exploración via
        /// <see cref="IExplorationBehaviorService"/>.
        /// </summary>
        private bool TriggerBehavior(HeroBehaviorSlot slot)
        {
            bool inCombat = ServiceLocator.TryGetService<IPhaseService>(out var phase)
                            && phase != null
                            && phase.CurrentBase != GamePhase.Exploration;

            if (inCombat)
            {
                var buttons = FindFirstObjectByType<PlayerActionButtonsView>(FindObjectsInactive.Include);
                return buttons != null && buttons.TryTriggerSlot(slot);
            }

            if (ServiceLocator.TryGetService<IExplorationBehaviorService>(out var exploration)
                && exploration != null)
            {
                exploration.OnBehaviorSelected((int)slot);
                return true;
            }
            return false;
        }

        // ==================================================================
        // Seleccion de objetivo
        // ==================================================================

        /// <summary>
        /// Abre el selector si algún efecto del <c>OnActivate</c> lo pide. Devuelve
        /// <c>true</c> si la activación queda pendiente del resultado.
        /// </summary>
        private bool TryBeginTargetSelection(ItemSO item, int slotIndex)
        {
            var settings = ResolveSelectionSettings(item);
            if (settings == null || settings.SlotState == SlotState.Self) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || grid == null
                || !grid.TryGetPosition(_playerGuid, out var ownerPos))
            {
                return false;
            }

            if (settings.AutoResolve)
            {
                var auto = settings.AutoResolveTargets(ownerPos, _playerGuid);
                ActivatePending(slotIndex, auto);
                return true;
            }

            var validTargets = settings.ResolveValidTiles(ownerPos, _playerGuid);
            if (validTargets == null || validTargets.Count == 0) return false;

            if (!ServiceLocator.TryGetService<ISelectionController>(out var controller) || controller == null)
                return false;

            _pendingSelectionIndex = slotIndex;
            controller.OnSelectionCompleted += HandleSelectionCompleted;
            controller.BeginSelection(new SelectionRequest
            {
                Settings = settings,
                ValidTargets = validTargets,
                OwnerGuid = _playerGuid,
                HighlightStyle = "move",
            });
            return true;
        }

        private static SelectionSettings ResolveSelectionSettings(ItemSO item)
        {
            var effects = item?.OnActivate?.Effects;
            if (effects == null) return null;
            for (int i = 0; i < effects.Count; i++)
            {
                var eff = effects[i];
                if (eff != null && eff.HasSelectionRequirement()) return eff.GetSelection();
            }
            return null;
        }

        private void HandleSelectionCompleted(TargetSelectionResult result)
        {
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller) && controller != null)
                controller.OnSelectionCompleted -= HandleSelectionCompleted;

            int slotIndex = _pendingSelectionIndex;
            _pendingSelectionIndex = -1;

            // Cancelar no gasta el item — el doc lo pide explicitamente.
            if (slotIndex < 0 || result == null || !result.WasCompleted) return;

            ActivatePending(slotIndex, result);
        }

        private void ActivatePending(int slotIndex, TargetSelectionResult result)
        {
            if (!ServiceLocator.TryGetService<IInventoryService>(out var inv) || inv == null) return;
            if (slotIndex < 0 || slotIndex >= inv.ActiveItems.Count) return;

            inv.ActivateItem(slotIndex, BuildContext(result));
        }

        private void CancelPendingSelection()
        {
            if (_pendingSelectionIndex < 0) return;
            _pendingSelectionIndex = -1;
            if (ServiceLocator.TryGetService<ISelectionController>(out var controller) && controller != null)
                controller.OnSelectionCompleted -= HandleSelectionCompleted;
        }

        /// <summary>
        /// Contexto de activación. Con <paramref name="result"/> null cae en self-target
        /// (el tile del jugador), que es lo que esperan los efectos con
        /// <c>SlotState = Self</c> como <c>EffHeal</c>.
        /// </summary>
        private EffectContext BuildContext(TargetSelectionResult result)
        {
            var ctx = new EffectContext
            {
                SourceGuid = _playerGuid,
                TargetGuid = _playerGuid,
                lastResult = true,
            };

            if (result != null)
            {
                ctx.SelectionResult = result;
                return ctx;
            }

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

        // ==================================================================
        // Rechazo
        // ==================================================================

        /// <summary>Motivo localizado del rechazo, para el toast.</summary>
        private static string DescribeBlock(ItemActivationBlock block)
        {
            switch (block)
            {
                case ItemActivationBlock.OnCooldown:
                    return LocalizedContent.Ui(UiTextKeys.RejectOnCooldown, "Todavía en enfriamiento.");
                case ItemActivationBlock.NotEnoughRolls:
                    return LocalizedContent.Ui(UiTextKeys.RejectNoRolls, "No te alcanzan los rolls.");
                case ItemActivationBlock.NotYourTurn:
                    return LocalizedContent.Ui(UiTextKeys.RejectNotYourTurn, "No es tu turno.");
                case ItemActivationBlock.ActionAlreadyUsed:
                    return LocalizedContent.Ui(UiTextKeys.RejectActionUsedThisTurn,
                                               "Ya usaste un objeto así este turno.");
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
    }
}
