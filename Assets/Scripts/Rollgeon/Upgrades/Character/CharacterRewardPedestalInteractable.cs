using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.State;
using Rollgeon.Grid;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// MonoBehaviour del pedestal de Character Reward — sin cobro (free pick).
    /// Mismo patrón de input/range/prompt que el shop pedestal y el altar de
    /// encantamiento. <see cref="WorldTooltipTrigger"/> muestra el reward al hover.
    /// </summary>
    /// <remarks>
    /// Al interactuar, llama a <see cref="ICharacterRewardService.NotifyPedestalClaimed"/>
    /// — el service aplica el modifier y destruye los pedestales hermanos
    /// (la elección es mutuamente exclusiva: 1 de 3).
    /// </remarks>
    [AddComponentMenu("Rollgeon/Upgrades/Character/Character Reward Pedestal")]
    public sealed class CharacterRewardPedestalInteractable : MonoBehaviour
    {
        private const string LogPrefix = "[CharacterRewardPedestal] ";

        [Tooltip("Label del prompt — rellenado en Configure.")]
        public string InteractLabel;

        [SerializeField] private float _interactRange = 1.5f;
        [SerializeField] private Key _interactKey = Key.F;
        [SerializeField] private WorldTooltipTrigger _tooltipTrigger;

        private Guid _roomInstanceId;
        private string _spawnPointId;
        private ICharacterRewardService _service;
        private CharacterRewardSO _reward;
        private bool _playerInRangeLastTick;

        public void Configure(Guid roomInstanceId, string spawnPointId, ICharacterRewardService service, CharacterRewardSO reward)
        {
            _roomInstanceId = roomInstanceId;
            _spawnPointId = spawnPointId;
            _service = service;
            _reward = reward;
            InteractLabel = BuildLabel(reward, _interactKey);

            EnsureTooltipRefs();
        }

        /// <summary>Free pick — sin precio, <see cref="InteractionPromptContent.Price"/> = -1.</summary>
        private InteractionPromptContent BuildPromptContent()
        {
            string title = _reward != null
                ? (!string.IsNullOrEmpty(_reward.DisplayName) ? _reward.DisplayName : _reward.UpgradeId)
                : string.Empty;
            string description = _reward != null ? (_reward.Description ?? string.Empty) : string.Empty;
            return new InteractionPromptContent(_interactKey.ToString(), "Tomar", title, description);
        }

        public void Interact()
        {
            if (_service == null || _reward == null)
            {
                Debug.LogWarning(LogPrefix + "Interact sin Configure — no-op.");
                return;
            }
            _service.NotifyPedestalClaimed(_roomInstanceId, _spawnPointId);
        }

        // ====================================================================
        // Update loop (input + range)
        // ====================================================================

        private void Update()
        {
            if (_interactRange <= 0f) return;
            if (_service == null) return;

            // BUG-017: si el state de este slot ya fue Claimed (otro pedestal del set
            // ganó esta ventana de input o un retry resolvió antes), no procesamos F.
            // El GameObject se destruye al final del frame pero su Update sigue corriendo;
            // sin este guard, una segunda interacción podría intentar entrar al service.
            if (IsClaimed())
            {
                if (_playerInRangeLastTick) UpdatePromptVisibility(false);
                _playerInRangeLastTick = false;
                return;
            }

            bool inRange = IsPlayerInRange();
            if (inRange != _playerInRangeLastTick)
            {
                _playerInRangeLastTick = inRange;
                UpdatePromptVisibility(inRange);
            }

            if (!inRange) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[_interactKey].wasPressedThisFrame) return;

            Interact();
        }

        private bool IsClaimed()
        {
            if (_roomInstanceId == Guid.Empty || string.IsNullOrEmpty(_spawnPointId)) return false;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return false;
            if (!dungeon.GetAllRoomInstances().TryGetValue(_roomInstanceId, out var room)) return false;
            return room.ObjectStates.TryGet<CharacterRewardState>(_spawnPointId, out var state)
                && state.Claimed;
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
        // Prompt + tooltip
        // ====================================================================

        private void EnsureTooltipRefs()
        {
            if (_tooltipTrigger == null) _tooltipTrigger = GetComponent<WorldTooltipTrigger>();
            if (_tooltipTrigger != null) _tooltipTrigger.TextProvider = BuildTooltipText;
        }

        private string BuildTooltipText()
        {
            if (_reward == null) return string.Empty;
            string name = !string.IsNullOrEmpty(_reward.DisplayName) ? _reward.DisplayName : _reward.UpgradeId;
            string desc = _reward.Description ?? string.Empty;
            return $"<b>{name}</b>\n<size=80%>{desc}</size>";
        }

        private static string BuildLabel(CharacterRewardSO reward, Key key)
        {
            if (reward == null) return $"[{key}] Tomar";
            string name = !string.IsNullOrEmpty(reward.DisplayName) ? reward.DisplayName : reward.UpgradeId;
            return $"[{key}] Tomar {name}";
        }

        private void UpdatePromptVisibility(bool visible)
        {
            if (visible)
            {
                InteractionPromptView.Show(GetInstanceID(), BuildPromptContent());
            }
            else
            {
                InteractionPromptView.Hide(GetInstanceID());
            }
        }

        private void OnDisable()
        {
            _playerInRangeLastTick = false;
            InteractionPromptView.Hide(GetInstanceID());
        }
    }
}
