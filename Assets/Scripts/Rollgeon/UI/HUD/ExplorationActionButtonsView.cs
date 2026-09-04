using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Exploration;
using Rollgeon.Heroes;
using Rollgeon.Input;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LocalizedContent = Rollgeon.Localization.LocalizedContent;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/Exploration Action Buttons View")]
    public class ExplorationActionButtonsView : MonoBehaviour
    {
        [Title("Action Buttons")]
        [InfoBox("Botones de accion para exploracion. Cada boton se mapea a un " +
                 "HeroBehaviorSlot via la lista paralela _slots — el contrato es " +
                 "por SLOT, no por list index. Eso evita que el orden de los " +
                 "buttons en la jerarquia desemboque en disparar el behavior " +
                 "equivocado (ej. button 'Pass Door' ejecutando 'Healing').")]
        [SerializeField]
        private List<Button> _buttons = new List<Button>();

        [InfoBox("Slot asignado a cada button (mismo index que _buttons). " +
                 "Movement=0, BaseAttack=1, ClassSkill=2, Healing=3, ForceDoor/PassDoor=4.")]
        [SerializeField]
        private List<HeroBehaviorSlot> _slots = new List<HeroBehaviorSlot>();

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private List<HeroActionBehavior> _activeBehaviors;
        private EventManager.EventReceiver _onPhaseEnter;
        private EventManager.EventReceiver _onPhaseExit;
        private EventManager.EventReceiver _onInventoryChanged;
        private IGameplayHotkeyService _hotkeys;

        private void Awake()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null) continue;
                int index = i;
                _buttons[i].onClick.AddListener(() => HandleClick(index));

                // Tap sobre un chip deshabilitado → cartel con el motivo (mismo
                // contrato que los chips de combate).
                var visual = _buttons[i].GetComponent<ChipButtonVisual>();
                if (visual != null) visual.OnBlockedPressed += _ => ShowRejectToast(index);
            }
        }

        private void OnDestroy()
        {
            foreach (var btn in _buttons)
            {
                if (btn == null) continue;
                btn.onClick.RemoveAllListeners();
                var visual = btn.GetComponent<ChipButtonVisual>();
                if (visual != null) visual.OnBlockedPressed = null;
            }
            if (_bound) Unbind();
        }

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            _onPhaseEnter = OnPhaseEnter;
            _onPhaseExit = OnPhaseExit;
            _onInventoryChanged = OnInventoryChanged;

            EventManager.Subscribe(EventName.OnPhaseEnter, _onPhaseEnter);
            EventManager.Subscribe(EventName.OnPhaseExit, _onPhaseExit);
            // Heal depende de PCHasInventoryItem(potion.healing). Si la pocion se
            // consume, el button tiene que pasar a no-interactable inmediatamente.
            EventManager.Subscribe(EventName.OnItemObtained, _onInventoryChanged);
            EventManager.Subscribe(EventName.OnItemRemoved, _onInventoryChanged);
            EventManager.Subscribe(EventName.OnActiveItemUsed, _onInventoryChanged);
            // Tutorial: refrescar interactables cuando un slot se desbloquea.
            EventManager.Subscribe(EventName.OnTutorialActionUnlocked, _onInventoryChanged);

            HookHotkeys(true);

            _bound = true;

            if (ServiceLocator.TryGetService<IPhaseService>(out var phase)
                && phase.CurrentBase == GamePhase.Exploration)
            {
                RefreshButtons();
                SetVisible(true);
            }
            else
            {
                SetVisible(false);
            }
        }

        public void Unbind()
        {
            if (!_bound) return;

            EventManager.UnSubscribe(EventName.OnPhaseEnter, _onPhaseEnter);
            EventManager.UnSubscribe(EventName.OnPhaseExit, _onPhaseExit);
            EventManager.UnSubscribe(EventName.OnItemObtained, _onInventoryChanged);
            EventManager.UnSubscribe(EventName.OnItemRemoved, _onInventoryChanged);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, _onInventoryChanged);
            EventManager.UnSubscribe(EventName.OnTutorialActionUnlocked, _onInventoryChanged);

            HookHotkeys(false);

            _bound = false;
            _activeBehaviors = null;
            SetVisible(false);
        }

        private void RefreshButtons()
        {
            _activeBehaviors = null;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)
                || playerService.CurrentHero == null)
                return;

            _activeBehaviors = playerService.CurrentHero.GetBehaviorsForPhase(GamePhase.Exploration);

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null) continue;
                var behavior = ResolveBehaviorForButton(i);
                bool hasEntry = behavior != null && !IsHiddenInExploration(i, behavior);
                _buttons[i].gameObject.SetActive(hasEntry);

                if (hasEntry)
                    _buttons[i].interactable = IsBehaviorAvailable(behavior);
            }
        }

        // El slot ForceDoor en Exploración resuelve al behavior "Pass door". La UX
        // ahora es seleccionar la casilla roja "frente a puerta" durante el movimiento
        // (ver ExplorationBehaviorService), así que el botón sobra y se oculta en este
        // HUD. Movement también se oculta: el modo movimiento está SIEMPRE activo en
        // Exploración (click-to-move permanente, auto-armado por
        // ExplorationBehaviorService), así que el botón ya no hace falta. Healing se
        // oculta por regla de diseño (sep 2026): curar es una acción de combate — gasta
        // el turno y compite con atacar — y curarse gratis entre salas vaciaba esa
        // decisión. El botón es la única entrada (el hotkey H pasa por acá), así que
        // ocultarlo cierra el camino entero. En combate la lógica vive en el HUD de
        // combate — éste sólo se muestra en Exploración, así que el filtro es local.
        private bool IsHiddenInExploration(int buttonIndex, HeroActionBehavior behavior)
        {
            if (behavior == null) return false;
            var slot = (_slots != null && buttonIndex < _slots.Count)
                ? _slots[buttonIndex]
                : behavior.Slot;
            return slot == HeroBehaviorSlot.ForceDoor
                || slot == HeroBehaviorSlot.Movement
                || slot == HeroBehaviorSlot.Healing;
        }

        private void RefreshInteractable()
        {
            if (_activeBehaviors == null) return;

            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null) continue;
                var behavior = ResolveBehaviorForButton(i);
                if (behavior != null)
                    _buttons[i].interactable = IsBehaviorAvailable(behavior);
            }
        }

        // Mapea button[i] al behavior cuyo Slot == _slots[i]. Si el user no
        // wireo _slots (lista vacia o mas corta), fallback a tomar el behavior
        // por list-index (legacy) — emite warning para que sea visible.
        private HeroActionBehavior ResolveBehaviorForButton(int i)
        {
            if (_activeBehaviors == null) return null;

            if (_slots != null && i < _slots.Count)
            {
                var slot = _slots[i];
                for (int j = 0; j < _activeBehaviors.Count; j++)
                {
                    if (_activeBehaviors[j] != null && _activeBehaviors[j].Slot == slot)
                        return _activeBehaviors[j];
                }
                return null;
            }

            Debug.LogWarning("[ExplorationActionButtonsView] _slots no esta wireado — " +
                             "fallback a list-index (puede disparar el behavior equivocado).");
            return i < _activeBehaviors.Count ? _activeBehaviors[i] : null;
        }

        private bool IsBehaviorAvailable(HeroActionBehavior behavior)
        {
            // Tutorial: slots todavía no desbloqueados quedan no-interactables.
            // Backstop de ejecución en TurnManager.IsForbiddenByRuleset.
            if (ServiceLocator.TryGetService<Rollgeon.Tutorial.ITutorialActionGateService>(out var tutorialGate)
                && tutorialGate != null && tutorialGate.IsSlotLocked(behavior.Slot))
                return false;

            // BUG-017: con la vida llena el heal no aporta nada (HealPipeline lo
            // clampea a 0) — el botón no debe ser interactable.
            if (behavior.Slot == HeroBehaviorSlot.Healing
                && !HealAvailability.CanHealMore(_playerGuid))
                return false;

            // Preconditions del behavior (ej. Heal requiere
            // PCHasInventoryItem(potion.healing)). Sin esto el boton de Heal queda
            // interactable despues de consumir la pocion. En exploración las acciones
            // son gratis — no hay gate de recursos.
            bool ok = behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out var reason);
            if (behavior.ActionName == "Force Door")
            {
                Debug.Log($"[ExplorationActionButtonsView] Force Door interactable={ok} reason='{reason}' " +
                          $"playerGuid={_playerGuid}");
            }
            return ok;
        }

        private void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        // ======================================================================
        // Toast de rechazo — "Esta acción no puede ser realizada" + motivo
        // ======================================================================

        private void ShowRejectToast(int buttonIndex)
        {
            if (buttonIndex < 0 || buttonIndex >= _buttons.Count) return;
            var button = _buttons[buttonIndex];
            if (button == null) return;

            string reason = ResolveRejectReason(buttonIndex);
            if (string.IsNullOrEmpty(reason)) return;

            string title = LocalizedContent.Ui(UiTextKeys.RejectTitle,
                "Esta acción no puede ser realizada");
            var label = button.GetComponentInChildren<TMP_Text>(true);
            ActionRejectToast.Show(button.transform as RectTransform,
                title + "\n" + reason, label != null ? label.font : null);
        }

        /// <summary>
        /// Motivo del rechazo en exploración, resuelto al momento del tap. Espejo
        /// del gating de <see cref="IsBehaviorAvailable"/>: vida llena → poción →
        /// condiciones. Null = gate del tutorial (el overlay ya guía).
        /// </summary>
        private string ResolveRejectReason(int buttonIndex)
        {
            var behavior = ResolveBehaviorForButton(buttonIndex);
            if (behavior == null) return null;

            if (ServiceLocator.TryGetService<Rollgeon.Tutorial.ITutorialActionGateService>(out var gate)
                && gate != null && gate.IsSlotLocked(behavior.Slot))
                return null;

            if (behavior.Slot == HeroBehaviorSlot.Healing
                && !HealAvailability.CanHealMore(_playerGuid))
                return LocalizedContent.Ui(UiTextKeys.RejectFullHealth, "Tienes la vida completa.");

            if (!behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out _))
            {
                // El único gate restante del heal en exploración es la poción
                // (PCHasInventoryItem(potion.healing)).
                if (behavior.Slot == HeroBehaviorSlot.Healing)
                    return LocalizedContent.Ui(UiTextKeys.RejectNoPotion, "No tienes poción disponible.");
                return LocalizedContent.Ui(UiTextKeys.RejectNoRange, "Sin rango al objetivo.");
            }

            return null;
        }

        // ======================================================================
        // Hotkeys (teclado) — mirror del click, gateado por interactable
        // ======================================================================

        private void HookHotkeys(bool subscribe)
        {
            if (subscribe)
            {
                if (_hotkeys == null
                    && !ServiceLocator.TryGetService<IGameplayHotkeyService>(out _hotkeys))
                    return;
            }
            if (_hotkeys == null) return;

            if (subscribe)
            {
                // En Exploración el botón de Movement está oculto (click-to-move
                // permanente), así que Q cae en un botón inactivo → no-op. Igual lo
                // suscribimos por consistencia con el mapeo global de teclas.
                _hotkeys.Subscribe(GameplayHotkey.Move, OnHotkeyMove);
                _hotkeys.Subscribe(GameplayHotkey.Attack, OnHotkeyAttack);
                _hotkeys.Subscribe(GameplayHotkey.ClassSkill, OnHotkeyClassSkill);
                // Healing también está oculto en exploración (solo se cura en combate),
                // así que H cae en botón inactivo → no-op.
                _hotkeys.Subscribe(GameplayHotkey.Heal, OnHotkeyHeal);
                // Force Door en exploración se dispara por la casilla roja (botón oculto),
                // así que F cae en botón inactivo → no-op. Se suscribe por consistencia.
                _hotkeys.Subscribe(GameplayHotkey.ForceDoor, OnHotkeyForceDoor);
            }
            else
            {
                _hotkeys.Unsubscribe(GameplayHotkey.Move, OnHotkeyMove);
                _hotkeys.Unsubscribe(GameplayHotkey.Attack, OnHotkeyAttack);
                _hotkeys.Unsubscribe(GameplayHotkey.ClassSkill, OnHotkeyClassSkill);
                _hotkeys.Unsubscribe(GameplayHotkey.Heal, OnHotkeyHeal);
                _hotkeys.Unsubscribe(GameplayHotkey.ForceDoor, OnHotkeyForceDoor);
                _hotkeys = null;
            }
        }

        private void OnHotkeyMove(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.Movement);
        private void OnHotkeyAttack(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.BaseAttack);
        private void OnHotkeyClassSkill(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.ClassSkill);
        private void OnHotkeyHeal(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.Healing);
        private void OnHotkeyForceDoor(InputAction.CallbackContext _) => TriggerSlotHotkey(HeroBehaviorSlot.ForceDoor);

        // Invoca el onClick del botón cuyo slot (via _slots) matchea, solo si está
        // activo e interactable → mismo camino y gating que un click real. Si está
        // visible pero deshabilitado, responde con el mismo rechazo completo que el
        // mouse (shake + SFX + toast vía ChipButtonVisual.TryRejectPress). Los
        // botones ocultos (Movement/Healing/ForceDoor en exploración) siguen siendo no-op.
        private void TriggerSlotHotkey(HeroBehaviorSlot slot)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                if (btn == null) continue;
                var s = (_slots != null && i < _slots.Count) ? _slots[i] : (HeroBehaviorSlot)i;
                if (s != slot) continue;
                if (!btn.gameObject.activeInHierarchy) return;
                if (btn.interactable)
                {
                    btn.onClick.Invoke();
                    return;
                }
                var visual = btn.GetComponent<ChipButtonVisual>();
                if (visual != null) visual.TryRejectPress();
                return;
            }
        }

        private void HandleClick(int buttonIndex)
        {
            if (!ServiceLocator.TryGetService<IExplorationBehaviorService>(out var service)) return;

            // Mapeo button → slot via _slots[buttonIndex]. El service interpreta
            // el int como HeroBehaviorSlot, no como list-index.
            int slot;
            if (_slots != null && buttonIndex < _slots.Count)
            {
                slot = (int)_slots[buttonIndex];
            }
            else
            {
                Debug.LogWarning("[ExplorationActionButtonsView] _slots no esta wireado — " +
                                 "usando buttonIndex como slot (legacy).");
                slot = buttonIndex;
            }

            service.OnBehaviorSelected(slot);
        }

        private void OnPhaseEnter(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if ((GamePhase)args[0] == GamePhase.Exploration)
            {
                RefreshButtons();
                SetVisible(true);
            }
        }

        private void OnPhaseExit(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if ((GamePhase)args[0] == GamePhase.Exploration)
            {
                _activeBehaviors = null;
                SetVisible(false);
            }
        }

        private void OnInventoryChanged(params object[] args)
        {
            if (!_bound) return;
            RefreshInteractable();
        }
    }
}
