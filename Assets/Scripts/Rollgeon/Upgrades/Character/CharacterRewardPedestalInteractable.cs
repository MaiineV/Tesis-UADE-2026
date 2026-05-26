using System;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Player;
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
        [SerializeField] private GameObject _promptVisual;
        [SerializeField] private TMPro.TextMeshProUGUI _promptLabel;
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

            EnsurePromptRefs();
            EnsureTooltipRefs();
            UpdatePromptVisibility(false);
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

        private void EnsurePromptRefs()
        {
            if (_promptVisual == null)
            {
                var t = transform.Find("Prompt");
                if (t != null) _promptVisual = t.gameObject;
            }
            if (_promptVisual == null) _promptVisual = BuildAutoPrompt();
            if (_promptLabel == null && _promptVisual != null)
            {
                _promptLabel = _promptVisual.GetComponentInChildren<TMPro.TextMeshProUGUI>(includeInactive: true);
            }
        }

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

        private GameObject BuildAutoPrompt()
        {
            var promptGo = new GameObject("Prompt");
            promptGo.transform.SetParent(transform, worldPositionStays: false);
            promptGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            promptGo.transform.localRotation = Quaternion.identity;
            promptGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var canvas = promptGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1;
            promptGo.AddComponent<UnityEngine.UI.CanvasScaler>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(promptGo.transform, worldPositionStays: false);
            var rt = labelGo.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400f, 80f);
            rt.localPosition = Vector3.zero;

            var tmp = labelGo.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = 32f;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.text = string.Empty;
            tmp.raycastTarget = false;

            promptGo.SetActive(false);
            return promptGo;
        }

        private void UpdatePromptVisibility(bool visible)
        {
            if (_promptVisual != null) _promptVisual.SetActive(visible);
            if (_promptLabel != null && visible) _promptLabel.text = InteractLabel ?? string.Empty;
        }

        private void OnDisable()
        {
            _playerInRangeLastTick = false;
            if (_promptVisual != null) _promptVisual.SetActive(false);
        }
    }
}
