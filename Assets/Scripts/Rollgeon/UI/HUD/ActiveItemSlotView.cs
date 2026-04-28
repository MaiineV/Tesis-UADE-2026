using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Un slot visual de item activo (arco / pocion / etc.). No se suscribe a eventos:
    /// <see cref="ActiveItemsView"/> lo controla via <see cref="SetState"/>. Si tiene un
    /// <see cref="_button"/> cableado, expone <see cref="OnClicked"/> para que el
    /// <c>ActiveItemsView</c> dispare la activación del ítem en el inventario.
    /// Plan §4.7.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Active Item Slot View")]
    public class ActiveItemSlotView : MonoBehaviour
    {
        [Title("Slot — Widget refs")]
        [Required("Arrastrar la Image del icono principal.")]
        [SerializeField]
        private Image _icon;

        [Tooltip("Button opcional para activar el ítem por click. Si null, el slot " +
                 "es solo display (estado, no clickable).")]
        [SerializeField]
        private Button _button;

        /// <summary>
        /// Disparado cuando el jugador clickea el slot y el estado actual es
        /// <see cref="ActiveItemState.Active"/>. <see cref="ActiveItemsView"/> se
        /// suscribe para invocar <c>IInventoryService.ActivateItem</c>.
        /// </summary>
        public event Action<ActiveItemSlotView> OnClicked;

        [Tooltip("GameObject overlay para estado Inactive. Opcional (puede ser null).")]
        [SerializeField]
        private GameObject _inactiveOverlay;

        [Tooltip("GameObject overlay para estado Depleted. Opcional.")]
        [SerializeField]
        private GameObject _depletedOverlay;

        [Title("Slot — Sprites (opcional)")]
        [Tooltip("Sprite que muestra cuando el slot esta Active. Si null, se conserva el actual.")]
        [SerializeField]
        private Sprite _iconActive;

        [Tooltip("Sprite para Inactive. Si null, se conserva el actual.")]
        [SerializeField]
        private Sprite _iconInactive;

        [ShowInInspector, ReadOnly]
        public ActiveItemState CurrentState { get; private set; } = ActiveItemState.Inactive;

        private void OnEnable()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(HandleClick);
                RefreshInteractable();
            }
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(HandleClick);
            }
        }

        private void HandleClick()
        {
            if (CurrentState != ActiveItemState.Active) return;
            OnClicked?.Invoke(this);
        }

        private void RefreshInteractable()
        {
            if (_button != null)
            {
                _button.interactable = CurrentState == ActiveItemState.Active;
            }
        }

        /// <summary>
        /// Togglea overlays y (opcional) swap de sprites segun el estado. Idempotente.
        /// </summary>
        public void SetState(ActiveItemState state)
        {
            CurrentState = state;

            if (_inactiveOverlay != null)
            {
                _inactiveOverlay.SetActive(state == ActiveItemState.Inactive);
            }
            if (_depletedOverlay != null)
            {
                _depletedOverlay.SetActive(state == ActiveItemState.Depleted);
            }

            if (_icon != null)
            {
                if (state == ActiveItemState.Active && _iconActive != null)
                {
                    _icon.sprite = _iconActive;
                }
                else if (state == ActiveItemState.Inactive && _iconInactive != null)
                {
                    _icon.sprite = _iconInactive;
                }
                // Depleted: conserva el sprite actual; el DepletedOverlay lo distingue.
            }

            RefreshInteractable();
        }
    }
}
