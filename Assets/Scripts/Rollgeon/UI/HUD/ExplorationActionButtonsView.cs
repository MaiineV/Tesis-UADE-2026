using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Exploration;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

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
                 "Movement=0, BaseAttack=1, SpecialAttack=2, Healing=3, ForceDoor/PassDoor=4.")]
        [SerializeField]
        private List<HeroBehaviorSlot> _slots = new List<HeroBehaviorSlot>();

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private List<HeroActionBehavior> _activeBehaviors;
        private EventManager.EventReceiver _onPhaseEnter;
        private EventManager.EventReceiver _onPhaseExit;
        private EventManager.EventReceiver _onEnergyChanged;
        private EventManager.EventReceiver _onInventoryChanged;

        private void Awake()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i] == null) continue;
                int index = i;
                _buttons[i].onClick.AddListener(() => HandleClick(index));
            }
        }

        private void OnDestroy()
        {
            foreach (var btn in _buttons)
            {
                if (btn != null) btn.onClick.RemoveAllListeners();
            }
            if (_bound) Unbind();
        }

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            _onPhaseEnter = OnPhaseEnter;
            _onPhaseExit = OnPhaseExit;
            _onEnergyChanged = OnEnergyChanged;
            _onInventoryChanged = OnInventoryChanged;

            EventManager.Subscribe(EventName.OnPhaseEnter, _onPhaseEnter);
            EventManager.Subscribe(EventName.OnPhaseExit, _onPhaseExit);
            EventManager.Subscribe(EventName.OnEnergyChanged, _onEnergyChanged);
            // Heal depende de PCHasInventoryItem(potion.healing). Si la pocion se
            // consume, el button tiene que pasar a no-interactable inmediatamente.
            EventManager.Subscribe(EventName.OnItemObtained, _onInventoryChanged);
            EventManager.Subscribe(EventName.OnItemRemoved, _onInventoryChanged);
            EventManager.Subscribe(EventName.OnActiveItemUsed, _onInventoryChanged);

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
            EventManager.UnSubscribe(EventName.OnEnergyChanged, _onEnergyChanged);
            EventManager.UnSubscribe(EventName.OnItemObtained, _onInventoryChanged);
            EventManager.UnSubscribe(EventName.OnItemRemoved, _onInventoryChanged);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, _onInventoryChanged);

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
                bool hasEntry = behavior != null;
                _buttons[i].gameObject.SetActive(hasEntry);

                if (hasEntry)
                    _buttons[i].interactable = IsBehaviorAvailable(behavior);
            }
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
            // Combina dos chequeos: energia suficiente Y preconditions del behavior
            // (ej. Heal requiere PCHasInventoryItem(potion.healing)). Sin esto el
            // boton de Heal queda interactable despues de consumir la pocion.
            return behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out _);
        }

        private void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
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

        private void OnEnergyChanged(params object[] args)
        {
            if (!_bound) return;
            RefreshInteractable();
        }

        private void OnInventoryChanged(params object[] args)
        {
            if (!_bound) return;
            RefreshInteractable();
        }
    }
}
