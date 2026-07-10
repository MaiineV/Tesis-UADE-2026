using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Grid;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// MonoBehaviour del altar de la Sala de Encantamiento. Patrón calcado de
    /// <c>ShopItemPedestalInteractable</c>: input <c>F</c> + check de rango +
    /// prompt 2D via <see cref="InteractionPromptView"/> + tooltip hover via
    /// <see cref="WorldTooltipTrigger"/>.
    /// </summary>
    /// <remarks>
    /// Mientras no aterrice el <c>IInteractionService</c> (§7.7), este componente
    /// orquesta su propio input. Al presionar <c>F</c> dentro del rango, llama
    /// a <c>service.NotifyAltarActivated</c> — el service emite el evento que la
    /// UI (Phase 6) consume para abrir la pantalla de selección.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/Enchantment Altar Interactable")]
    public sealed class EnchantmentAltarInteractable : MonoBehaviour
    {
        private const string LogPrefix = "[EnchantmentAltarInteractable] ";

        [Tooltip("Label mostrado en el prompt. Se rellena desde Configure — no editar a mano en prefab.")]
        public string InteractLabel;

        [Tooltip("Distancia (world units) máxima a la que el player puede interactuar. 0 desactiva la interacción.")]
        [SerializeField]
        private float _interactRange = 1.5f;

        [Tooltip("Tecla que dispara el flow de encantamiento cuando el player está en rango. Default F.")]
        [SerializeField]
        private Key _interactKey = Key.F;

        [Tooltip("Tooltip trigger opcional. Si está, se rellena con el texto de costo para hover.")]
        [SerializeField]
        private WorldTooltipTrigger _tooltipTrigger;

        private Guid _roomInstanceId;
        private string _spawnPointId;
        private IEnchantmentRoomService _service;
        private int _baseCost;
        private bool _playerInRangeLastTick;
        private bool _lastCanAfford;

        /// <summary>Inicializa el altar. Lo llama el <see cref="EnchantmentRoomService"/> al instanciarlo.</summary>
        public void Configure(Guid roomInstanceId, string spawnPointId, IEnchantmentRoomService service, int baseCost)
        {
            _roomInstanceId = roomInstanceId;
            _spawnPointId = spawnPointId;
            _service = service;
            _baseCost = baseCost;
            InteractLabel = $"[{_interactKey}] Encantar Dado ({_baseCost}G)";

            EnsureTooltipRefs();
            UpdatePromptVisibility(false);
        }

        /// <summary>
        /// Dispara el flow de encantamiento. Lo llama el Update loop al detectar <see cref="_interactKey"/>,
        /// o un test invocando directamente.
        /// </summary>
        public void Interact()
        {
            if (_service == null)
            {
                Debug.LogWarning(LogPrefix + "Interact invocado sin Configure previo — no-op.");
                return;
            }
            _service.NotifyAltarActivated(_roomInstanceId, _spawnPointId);
        }

        // ====================================================================
        // Update loop (input + range)
        // ====================================================================

        private void Update()
        {
            if (_interactRange <= 0f) return;
            if (_service == null) return;

            bool inRange = IsPlayerInRange();
            if (inRange != _playerInRangeLastTick)
            {
                _playerInRangeLastTick = inRange;
                UpdatePromptVisibility(inRange);
            }
            else if (inRange)
            {
                // El gold pudo cambiar estando en rango (ej. compró en otro pedestal) —
                // re-Show refresca el color del costo sin esperar un cambio de rango.
                bool canAfford = BuildPromptContent().CanAfford;
                if (canAfford != _lastCanAfford) UpdatePromptVisibility(true);
            }

            if (!inRange) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[_interactKey].wasPressedThisFrame) return;

            Interact();
        }

        private bool IsPlayerInRange()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null) return false;
            var playerGuid = playerService.PlayerGuid;
            if (playerGuid == Guid.Empty) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!grid.TryGetPosition(playerGuid, out var playerCoord)) return false;

            var playerWorld = grid.GridToWorld(playerCoord);
            float distSq = (playerWorld - transform.position).sqrMagnitude;
            return distSq <= _interactRange * _interactRange;
        }

        // ====================================================================
        // Prompt + tooltip setup
        // ====================================================================

        private void EnsureTooltipRefs()
        {
            if (_tooltipTrigger == null)
            {
                _tooltipTrigger = GetComponent<WorldTooltipTrigger>();
            }
            if (_tooltipTrigger != null)
            {
                _tooltipTrigger.TextProvider = BuildTooltipText;
            }
        }

        private string BuildTooltipText()
        {
            return $"Altar de Encantamiento\nCosto base: {_baseCost} oro\nModifica las caras posibles de un dado.";
        }

        /// <summary>
        /// Arma el contenido del <see cref="InteractionPromptView"/> — mismo patrón que
        /// <c>ShopItemPedestalInteractable</c>: el costo se pinta rojo si el oro no alcanza.
        /// </summary>
        private InteractionPromptContent BuildPromptContent()
        {
            bool canAfford = !ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null
                || economy.CanAfford(_baseCost);

            return new InteractionPromptContent(
                _interactKey.ToString(),
                "Encantar Dado",
                "Altar de Encantamiento",
                "Modifica las caras posibles de un dado.",
                _baseCost,
                canAfford);
        }

        private void UpdatePromptVisibility(bool visible)
        {
            if (!visible)
            {
                InteractionPromptView.Hide(GetInstanceID());
                return;
            }
            var content = BuildPromptContent();
            _lastCanAfford = content.CanAfford;
            InteractionPromptView.Show(GetInstanceID(), content);
        }

        private void OnDisable()
        {
            _playerInRangeLastTick = false;
            InteractionPromptView.Hide(GetInstanceID());
        }
    }
}
