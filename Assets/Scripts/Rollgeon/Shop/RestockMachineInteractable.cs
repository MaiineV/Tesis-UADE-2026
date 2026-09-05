using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Grid;
using Rollgeon.Localization;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Shop
{
    /// <summary>
    /// La máquina de reroll de la tienda (la ruleta, §17.F.5). Patrón calcado de
    /// <c>EnchantmentAltarInteractable</c>: input <c>F</c> + rango XZ al borde del
    /// visual + prompt via <see cref="InteractionPromptView"/> + tooltip hover.
    /// Al usarse, la ruleta gira (tween de yaw) y delega el cobro + re-roll en
    /// <see cref="IShopManagerService.TryRestock"/>. Agotada (sin usos), el prompt
    /// desaparece del todo.
    /// </summary>
    [AddComponentMenu("Rollgeon/Shop/Restock Machine Interactable")]
    public sealed class RestockMachineInteractable : MonoBehaviour, Rollgeon.UI.Cursor.ICursorHoverable
    {
        private const string LogPrefix = "[RestockMachineInteractable] ";

        [Tooltip("Distancia (world units) máxima al BORDE del visual. 0 desactiva la interacción.")]
        [SerializeField] private float _interactRange = 1.5f;

        [Tooltip("Tecla que dispara el reroll cuando el player está en rango. Default F.")]
        [SerializeField] private Key _interactKey = Key.F;

        [Tooltip("Transform que gira al usar la máquina (la ruleta). Null = el propio root.")]
        [SerializeField] private Transform _spinRoot;

        [Tooltip("Vueltas del giro al usarse.")]
        [SerializeField] private float _spinTurns = 2f;

        [SerializeField] private float _spinSeconds = 0.9f;

        [Tooltip("Tooltip trigger opcional para hover.")]
        [SerializeField] private WorldTooltipTrigger _tooltipTrigger;

        private Guid _roomInstanceId;
        private IShopManagerService _service;
        private bool _playerInRangeLastTick;
        private int _lastShownCost = -1;
        private bool _lastCanAfford;
        private bool _spinning;

        public void Configure(Guid roomInstanceId, IShopManagerService service)
        {
            _roomInstanceId = roomInstanceId;
            _service = service;

            if (_tooltipTrigger == null) _tooltipTrigger = GetComponent<WorldTooltipTrigger>();
            if (_tooltipTrigger != null) _tooltipTrigger.TextProvider = BuildTooltipText;

            UpdatePromptVisibility(false);
        }

        /// <summary>Dispara el reroll. Lo llama el Update al detectar F, o el click del prompt.</summary>
        public void Interact()
        {
            if (_service == null)
            {
                Debug.LogWarning(LogPrefix + "Interact sin Configure previo — no-op.");
                return;
            }
            if (_spinning) return;

            if (!_service.TryRestock(_roomInstanceId))
            {
                // Sin oro o sin usos: el prompt ya lo pinta (rojo / oculto); log de cortesía.
                Debug.Log(LogPrefix + "Reroll rechazado (oro insuficiente o sin usos).");
                return;
            }

            PlaySpin();

            // El costo del próximo uso subió (o la máquina se agotó): refrescar ya.
            _playerInRangeLastTick = false;
        }

        private void PlaySpin()
        {
            if (!Application.isPlaying || Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion)
                return;

            var target = _spinRoot != null ? _spinRoot : transform;
            _spinning = true;
            PrimeTween.Tween.LocalEulerAngles(target,
                    target.localEulerAngles,
                    target.localEulerAngles + new Vector3(0f, 360f * _spinTurns, 0f),
                    _spinSeconds, PrimeTween.Ease.OutCubic)
                .OnComplete(this, self => self._spinning = false);
        }

        private void Update()
        {
            if (_interactRange <= 0f || _service == null) return;

            // Agotada: nada que ofrecer — prompt afuera y F muda.
            if (!_service.CanRestock(_roomInstanceId))
            {
                if (_playerInRangeLastTick)
                {
                    _playerInRangeLastTick = false;
                    InteractionPromptView.Hide(GetInstanceID());
                }
                return;
            }

            bool inRange = IsPlayerInRange();
            if (inRange != _playerInRangeLastTick)
            {
                _playerInRangeLastTick = inRange;
                UpdatePromptVisibility(inRange);
            }
            else if (inRange)
            {
                // El oro o el costo pudieron cambiar en rango (compró algo, rerolleó).
                var content = BuildPromptContent();
                if (content.CanAfford != _lastCanAfford || _lastShownCost != _service.GetRestockCost(_roomInstanceId))
                    UpdatePromptVisibility(true);
            }

            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (!keyboard[_interactKey].wasPressedThisFrame) return;

            if (!inRange)
            {
                Debug.Log(LogPrefix + $"{_interactKey} fuera de rango de la máquina.");
                return;
            }

            Interact();
        }

        private bool IsPlayerInRange()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null) return false;
            var playerGuid = playerService.PlayerGuid;
            if (playerGuid == Guid.Empty) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            if (!grid.TryGetPosition(playerGuid, out var playerCoord)) return false;

            // Mismo criterio que el altar: distancia XZ al borde del visual, para que el
            // rango no dependa del pivot del modelo ni de su altura.
            var playerWorld = grid.GridToWorld(playerCoord);
            var closest = ClosestVisualPointXZ(playerWorld);
            float dx = playerWorld.x - closest.x;
            float dz = playerWorld.z - closest.z;
            return dx * dx + dz * dz <= _interactRange * _interactRange;
        }

        private Bounds _visualBounds;
        private bool _visualBoundsResolved;

        private Vector3 ClosestVisualPointXZ(Vector3 from)
        {
            if (!_visualBoundsResolved)
            {
                _visualBoundsResolved = true;
                bool found = false;
                foreach (var rend in GetComponentsInChildren<Renderer>())
                {
                    if (rend == null) continue;
                    if (!found) { _visualBounds = rend.bounds; found = true; }
                    else _visualBounds.Encapsulate(rend.bounds);
                }
                if (!found) _visualBounds = new Bounds(transform.position, Vector3.zero);
            }

            return new Vector3(
                Mathf.Clamp(from.x, _visualBounds.min.x, _visualBounds.max.x),
                0f,
                Mathf.Clamp(from.z, _visualBounds.min.z, _visualBounds.max.z));
        }

        private string BuildTooltipText()
        {
            string title = LocalizedContent.Ui("shop.restock.title", "Máquina de Reroll");
            string body = LocalizedContent.Ui("shop.restock.desc",
                "Cambia todos los ítems de la tienda por otros nuevos. Cada uso cuesta más.");
            return $"<b>{title}</b>\n{body}";
        }

        private InteractionPromptContent BuildPromptContent()
        {
            int cost = _service != null ? _service.GetRestockCost(_roomInstanceId) : 0;
            bool canAfford = !ServiceLocator.TryGetService<IEconomyService>(out var economy)
                || economy == null || economy.CanAfford(cost);

            return new InteractionPromptContent(
                _interactKey.ToString(),
                LocalizedContent.Ui("shop.restock.prompt", "Rerollear tienda"),
                LocalizedContent.Ui("shop.restock.title", "Máquina de Reroll"),
                LocalizedContent.Ui("shop.restock.desc",
                    "Cambia todos los ítems de la tienda por otros nuevos. Cada uso cuesta más."),
                cost,
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
            _lastShownCost = _service != null ? _service.GetRestockCost(_roomInstanceId) : -1;
            InteractionPromptView.Show(GetInstanceID(), content, Interact);
        }

        private void OnDisable()
        {
            _playerInRangeLastTick = false;
            InteractionPromptView.Hide(GetInstanceID());
        }
    }
}
