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
        [InfoBox("Botones de accion para exploracion. Cada boton mapea por indice " +
                 "a los behaviors retornados por GetBehaviorsForPhase(Exploration).")]
        [SerializeField]
        private List<Button> _buttons = new List<Button>();

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private List<HeroActionBehavior> _activeBehaviors;
        private EventManager.EventReceiver _onPhaseEnter;
        private EventManager.EventReceiver _onPhaseExit;
        private EventManager.EventReceiver _onEnergyChanged;

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

            EventManager.Subscribe(EventName.OnPhaseEnter, _onPhaseEnter);
            EventManager.Subscribe(EventName.OnPhaseExit, _onPhaseExit);
            EventManager.Subscribe(EventName.OnEnergyChanged, _onEnergyChanged);

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
                bool hasEntry = i < _activeBehaviors.Count;
                _buttons[i].gameObject.SetActive(hasEntry);

                if (hasEntry)
                    _buttons[i].interactable = HasEnoughEnergy(_activeBehaviors[i]);
            }
        }

        private void RefreshInteractable()
        {
            if (_activeBehaviors == null) return;

            for (int i = 0; i < _buttons.Count && i < _activeBehaviors.Count; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].interactable = HasEnoughEnergy(_activeBehaviors[i]);
            }
        }

        private bool HasEnoughEnergy(HeroActionBehavior behavior)
        {
            if (behavior.EnergyCost <= 0) return true;
            if (!ServiceLocator.TryGetService<IEnergyService>(out var energy)) return true;
            return energy.GetCurrent(_playerGuid) >= behavior.EnergyCost;
        }

        private void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void HandleClick(int index)
        {
            if (!ServiceLocator.TryGetService<IExplorationBehaviorService>(out var service)) return;
            service.OnBehaviorSelected(index);
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
    }
}
