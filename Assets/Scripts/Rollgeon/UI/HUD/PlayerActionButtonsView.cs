using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Phase;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/Player Action Buttons View")]
    public class PlayerActionButtonsView : MonoBehaviour
    {
        // ======================================================================
        // Serialized fields — behavior buttons
        // ======================================================================

        [Title("Behavior Buttons")]
        [SerializeField]
        private Button _movementButton;

        [SerializeField]
        private Button _attackButton;

        [SerializeField]
        private Button _specialButton;

        [SerializeField]
        private Button _healButton;
        
        [SerializeField]
        private Button _forceDoorButton;

        [Title("Energy Cost Labels")]
        [InfoBox("Labels TMP opcionales que muestran cuánta energía consume cada acción. " +
                 "Si dejás uno null, el costo no se muestra en ese botón.")]
        [SerializeField]
        private TextMeshProUGUI _movementCostLabel;

        [SerializeField]
        private TextMeshProUGUI _attackCostLabel;

        [SerializeField]
        private TextMeshProUGUI _specialCostLabel;

        [SerializeField]
        private TextMeshProUGUI _healCostLabel;

        [SerializeField] 
        private TextMeshProUGUI _forceDoorCostLabel;

        [SerializeField]
        [Tooltip("Formato del label de costo. Default '{0}'. Ej: '{0}E', '-{0}', 'E{0}'.")]
        private string _costLabelFormat = "{0}";

        [SerializeField]
        [Tooltip("Texto mostrado cuando el costo es 0 (acción gratuita). Vacío oculta el label.")]
        private string _zeroCostText = "";

        [Title("Action Colors")]
        [InfoBox("Color de fondo de cada botón según el tipo de acción. Se aplica al Image " +
                 "del botón en Awake. Los desactivados se atenúan automáticamente vía Button.colors.")]
        [SerializeField]
        private Color _movementColor = new Color(0.27f, 0.55f, 0.95f, 1f);   // azul

        [SerializeField]
        private Color _attackColor = new Color(0.93f, 0.30f, 0.30f, 1f);     // rojo

        [SerializeField]
        private Color _specialColor = new Color(0.69f, 0.34f, 0.93f, 1f);    // violeta

        [SerializeField]
        private Color _healColor = new Color(0.32f, 0.82f, 0.45f, 1f);       // verde
        
        [SerializeField]
        private Color _forceDoorColor = new Color(0.32f, 0.82f, 0.45f, 1f);

        // ======================================================================
        // Serialized fields — confirm button
        // ======================================================================

        [Title("Confirm")]
        [Required("Arrastrar el boton de Confirm.")]
        [SerializeField]
        private Button _confirmButton;

        // ======================================================================
        // Events
        // ======================================================================

        [Title("Events")]
        [SerializeField]
        private UnityEvent _onConfirmPressed = new UnityEvent();

        public UnityEvent OnConfirmPressed => _onConfirmPressed;

        public Action<int> OnBehaviorSelected;

        // ======================================================================
        // Internal state
        // ======================================================================

        public enum ButtonPhase { Idle, WaitingForAction, Rolled }

        [ShowInInspector, ReadOnly]
        private ButtonPhase _phase = ButtonPhase.Idle;

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        // Cacheado en Bind() para poder unsuscribir el C# event en Unbind() incluso si el
        // ServiceLocator reemplaza el servicio entre medio.
        private IMovementService _movementService;

        // Tracking de acciones usadas en el turno actual. Resetea al disparar
        // OnTurnStarted (jugador) — no usamos TurnManager.WasUsedThisTurn directo
        // porque indexa por ActionName y queremos clave por slot 0-3.
        private readonly bool[] _usedInTurn = new bool[5];

        // ======================================================================
        // Lifecycle
        // ======================================================================

        private void Awake()
        {
            if (_movementButton != null) _movementButton.onClick.AddListener(() => HandleBehaviorClick(0));
            if (_attackButton != null) _attackButton.onClick.AddListener(() => HandleBehaviorClick(1));
            if (_specialButton != null) _specialButton.onClick.AddListener(() => HandleBehaviorClick(2));
            if (_healButton != null) _healButton.onClick.AddListener(() => HandleBehaviorClick(3));
            if (_forceDoorButton != null) _forceDoorButton.onClick.AddListener(() => HandleBehaviorClick(4));

            if (_confirmButton != null) _confirmButton.onClick.AddListener(HandleConfirmClick);

            ApplyActionColors();
        }

        /// <summary>
        /// Pinta el <see cref="UnityEngine.UI.Image.color"/> de cada botón con el color
        /// configurado en Inspector. Lo hace en Awake para no requerir setup manual de
        /// colores en el prefab.
        /// </summary>
        private void ApplyActionColors()
        {
            ApplyColor(_movementButton, _movementColor);
            ApplyColor(_attackButton, _attackColor);
            ApplyColor(_specialButton, _specialColor);
            ApplyColor(_healButton, _healColor);
            ApplyColor(_forceDoorButton, _forceDoorColor);
        }

        private static void ApplyColor(Button button, Color color)
        {
            if (button == null) return;
            if (button.targetGraphic is UnityEngine.UI.Image img)
            {
                img.color = color;
            }
        }

        private void OnDestroy()
        {
            if (_movementButton != null) _movementButton.onClick.RemoveAllListeners();
            if (_attackButton != null) _attackButton.onClick.RemoveAllListeners();
            if (_specialButton != null) _specialButton.onClick.RemoveAllListeners();
            if (_healButton != null) _healButton.onClick.RemoveAllListeners();
            if (_forceDoorButton != null) _forceDoorButton.onClick.RemoveAllListeners();

            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(HandleConfirmClick);
        }

        private void OnDisable()
        {
            if (_bound) Unbind();
        }

        // ======================================================================
        // Public API
        // ======================================================================

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            EventManager.Subscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.Subscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.Subscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.Subscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.Subscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.Subscribe(EventName.OnPlayerEnergyChanged, HandlePlayerEnergyChanged);

            // Recalcula availability cuando alguien se mueve — habilita el Attack si te
            // acercaste al enemigo, o lo deshabilita si el enemigo se alejó. Sin esta
            // suscripción, el botón quedaba con el estado computado al inicio del turno.
            if (ServiceLocator.TryGetService<IMovementService>(out var movement) && movement != null)
            {
                _movementService = movement;
                _movementService.OnEntityMoved += HandleEntityMoved;
            }

            _bound = true;

            RefreshCostLabels();

            if (ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder)
                && turnOrder.ParticipantCount > 0
                && turnOrder.Current == _playerGuid)
            {
                _phase = ButtonPhase.WaitingForAction;
            }
            else
            {
                _phase = ButtonPhase.Idle;
            }
            RefreshInteractable();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnTurnStarted, HandleTurnStarted);
            EventManager.UnSubscribe(EventName.OnTurnFinished, HandleTurnFinished);
            EventManager.UnSubscribe(EventName.OnDiceRolled, HandleDiceRolled);
            EventManager.UnSubscribe(EventName.OnRollResolved, HandleRollResolved);
            EventManager.UnSubscribe(EventName.OnItemObtained, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnItemRemoved, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnActiveItemUsed, HandleInventoryChanged);
            EventManager.UnSubscribe(EventName.OnPlayerEnergyChanged, HandlePlayerEnergyChanged);

            if (_movementService != null)
            {
                _movementService.OnEntityMoved -= HandleEntityMoved;
                _movementService = null;
            }

            _bound = false;
            _phase = ButtonPhase.Idle;
            RefreshInteractable();
        }

        private void HandleInventoryChanged(params object[] args)
        {
            // Recalcula availability — el Heal queda gris si te quedaste sin pociones.
            RefreshInteractable();
        }

        private void HandlePlayerEnergyChanged(params object[] args)
        {
            // Schema: [Guid entityId, int current, int max]. Recalcula gating por EnergyCost
            // (ej: gastaste energía con un Attack y ahora el Heal ya no alcanza).
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            RefreshInteractable();
        }

        private void HandleEntityMoved(Guid entity, GridCoord from, GridCoord to, IReadOnlyList<GridCoord> path)
        {
            // Cualquier movimiento puede cambiar la disponibilidad de los botones del player:
            // - Si el player se mueve, sus selections (range-based) se recalculan.
            // - Si un enemigo se mueve, ahora puede estar en/fuera de rango del Attack.
            // El gate de _phase dentro de RefreshInteractable evita que esto habilite botones
            // fuera del turno del player.
            RefreshInteractable();
        }

        public void RefreshInteractable()
        {
            bool behaviors = false;
            bool confirm = false;

            switch (_phase)
            {
                case ButtonPhase.Idle:
                    break;
                case ButtonPhase.WaitingForAction:
                    behaviors = true;
                    break;
                case ButtonPhase.Rolled:
                    confirm = true;
                    break;
            }

            // Cada botón se habilita sólo si la fase lo permite, no se usó en este turno
            // Y la behavior puede ejecutar (preconditions OK — ej. heal con poción disponible).
            if (_movementButton != null)
                _movementButton.interactable = behaviors && !_usedInTurn[0] && IsBehaviorAvailable(HeroBehaviorSlot.Movement);
            if (_attackButton != null)
                _attackButton.interactable = behaviors && !_usedInTurn[1] && IsBehaviorAvailable(HeroBehaviorSlot.BaseAttack);
            if (_specialButton != null)
                _specialButton.interactable = behaviors && !_usedInTurn[2] && IsBehaviorAvailable(HeroBehaviorSlot.SpecialAttack);
            if (_healButton != null)
                _healButton.interactable = behaviors && !_usedInTurn[3] && IsBehaviorAvailable(HeroBehaviorSlot.Healing);
            if (_forceDoorButton != null)
                _forceDoorButton.interactable = behaviors && !_usedInTurn[4] && IsBehaviorAvailable(HeroBehaviorSlot.ForceDoor);

            if (_confirmButton != null) _confirmButton.interactable = confirm;
        }

        private bool IsBehaviorAvailable(HeroBehaviorSlot slot)
        {
            // Forzar Puerta no aplica en salas de Boss — el boss debe vencerse, no se escapa.
            if (slot == HeroBehaviorSlot.ForceDoor && IsCurrentRoomBoss()) return false;

            // Forzar Puerta requiere estar adyacente (Manhattan ≤ 1, ortogonal) a alguna
            // puerta de la sala. Refrescamos la disponibilidad en OnEntityMoved.
            if (slot == HeroBehaviorSlot.ForceDoor
                && !Rollgeon.Effects.Concretes.EffForceDoor.IsPlayerAdjacentToAnyDoor(_playerGuid))
            {
                return false;
            }

            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps?.CurrentHero == null) return true;
            var behavior = ps.CurrentHero.ResolveBaseBehavior(slot, GamePhase.Combat);
            if (behavior == null) return false;
            return behavior.HasUsableEffectGroup(_playerGuid, Guid.Empty, out _);
        }

        private static bool IsCurrentRoomBoss()
        {
            return ServiceLocator.TryGetService<Rollgeon.Dungeon.IDungeonService>(out var dungeon)
                   && dungeon?.CurrentRoom != null
                   && dungeon.CurrentRoom.Type == Rollgeon.Dungeon.RoomType.Boss;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void HandleTurnStarted(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            // Turno nuevo del jugador: resetear flags de uso. TurnManager hace lo
            // mismo internamente con _actionsUsedThisTurn.
            for (int i = 0; i < _usedInTurn.Length; i++) _usedInTurn[i] = false;
            _phase = ButtonPhase.WaitingForAction;
            RefreshInteractable();
        }

        private void HandleTurnFinished(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.Idle;
            RefreshInteractable();
        }

        private void HandleDiceRolled(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.Rolled;
            RefreshInteractable();
        }

        private void HandleRollResolved(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid != _playerGuid) return;
            _phase = ButtonPhase.WaitingForAction;
            RefreshInteractable();
        }

        // ======================================================================
        // Click handlers
        // ======================================================================

        private void HandleBehaviorClick(int index)
        {
            OnBehaviorSelected?.Invoke(index);

            // Marcamos el slot como usado en este turno y refrescamos. La cancelación
            // del effect chain (ej. precondición falla) no rollbackea visualmente —
            // para FP lo aceptamos: si el usuario clickea un botón válido, asumimos
            // que va a ejecutarse o dará feedback explícito de error.
            if (index >= 0 && index < _usedInTurn.Length)
            {
                _usedInTurn[index] = true;
                RefreshInteractable();
            }
        }

        private void HandleConfirmClick()
        {
            // Si hay un ActionRoll activo (Heal / Forzar Puerta), Confirm = resolver la
            // tirada actual via el service. NO disparar el flow normal de combate
            // (CombatHandoffService.OnConfirmRequested) — eso ejecutaría el behavior dos veces.
            if (ServiceLocator.TryGetService<Rollgeon.ActionRolls.IActionRollService>(out var rs)
                && rs != null && rs.IsActive)
            {
                rs.DeclineReroll();
                return;
            }
            _onConfirmPressed?.Invoke();
        }

        // ======================================================================
        // Energy cost labels
        // ======================================================================

        /// <summary>
        /// Pinta cada label de costo con el <see cref="HeroActionBehavior.EnergyCost"/>
        /// del behavior que mapea a su slot. Llamado en <see cref="Bind"/>.
        /// </summary>
        public void RefreshCostLabels()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService)
                || playerService?.CurrentHero == null)
            {
                ApplyCostText(_movementCostLabel, null);
                ApplyCostText(_attackCostLabel, null);
                ApplyCostText(_specialCostLabel, null);
                ApplyCostText(_healCostLabel, null);
                ApplyCostText(_forceDoorCostLabel, null);
                return;
            }

            var hero = playerService.CurrentHero;
            ApplyCostText(_movementCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.Movement, GamePhase.Combat));
            ApplyCostText(_attackCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.BaseAttack, GamePhase.Combat));
            ApplyCostText(_specialCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.SpecialAttack, GamePhase.Combat));
            ApplyCostText(_healCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.Healing, GamePhase.Combat));
            ApplyCostText(_forceDoorCostLabel,
                hero.ResolveBaseBehavior(HeroBehaviorSlot.ForceDoor, GamePhase.Combat));
        }

        private void ApplyCostText(TextMeshProUGUI label, HeroActionBehavior behavior)
        {
            if (label == null) return;
            if (behavior == null)
            {
                label.text = string.Empty;
                return;
            }

            int cost = ResolveDisplayCost(behavior);
            label.text = cost <= 0
                ? _zeroCostText
                : string.Format(_costLabelFormat, cost);
        }

        // Si el behavior tiene un IActionRollEffect, el cobro real lo hace el
        // IActionRollService con el cost del spec — el behavior.EnergyCost queda
        // engañoso (los wirings legacy lo ponen en 2 cuando el real es 1). Para
        // que el label refleje lo que efectivamente se va a cobrar, priorizamos
        // el spec del effect.
        private int ResolveDisplayCost(HeroActionBehavior behavior)
        {
            if (TryFindActionRollSpec(behavior, out var spec))
                return spec.EnergyCost;
            return behavior.EnergyCost;
        }

        private bool TryFindActionRollSpec(HeroActionBehavior behavior, out ActionRollSpec spec)
        {
            spec = default;
            if (behavior?.Effects == null) return false;
            foreach (var group in behavior.Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is IActionRollEffect rollEffect
                        && rollEffect.TryGetRollSpec(_playerGuid, out spec))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
