using System;
using Sirenix.OdinInspector;
using TMPro;
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

        [Title("Counter (opcional)")]
        [Tooltip("Label TMP para mostrar la cantidad del ítem (ej. 'x3'). Si null, se " +
                 "auto-crea uno como hijo en runtime cuando el conteo es > 1.")]
        [SerializeField]
        private TextMeshProUGUI _countLabel;

        [SerializeField]
        [Tooltip("Formato del label de cantidad. Default 'x{0}'.")]
        private string _countLabelFormat = "x{0}";

        [SerializeField]
        [Tooltip("Esconde el label cuando el conteo es <= este valor. Default 0 = sólo " +
                 "esconde cuando no tenés ítems. Subirlo a 1 si querés ocultar 'x1' (no " +
                 "mostrar contador con un solo ítem).")]
        private int _hideCountAtOrBelow = 0;

        [ShowInInspector, ReadOnly]
        public ActiveItemState CurrentState { get; private set; } = ActiveItemState.Inactive;

        private void Awake()
        {
            EnsureClickable();
            EnsureCountLabel();
        }

        /// <summary>
        /// Si <see cref="_countLabel"/> no está cableado en Inspector, lo busca como
        /// hijo (convención: nombre "CountLabel"). Si no existe, lo crea automáticamente
        /// en runtime con un TMP en la esquina inferior-derecha del slot.
        /// </summary>
        private void EnsureCountLabel()
        {
            if (_countLabel != null) return;
            var t = transform.Find("CountLabel");
            if (t != null) _countLabel = t.GetComponent<TextMeshProUGUI>();
            if (_countLabel == null) _countLabel = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (_countLabel == null) _countLabel = BuildAutoCountLabel();
        }

        /// <summary>
        /// Construye un TMP minimal como hijo del slot. Anchored bottom-right, font
        /// 28 con outline negro para legibilidad sobre el icono.
        /// </summary>
        private TextMeshProUGUI BuildAutoCountLabel()
        {
            var go = new GameObject("CountLabel");
            go.transform.SetParent(transform, worldPositionStays: false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-4f, 4f);
            rt.sizeDelta = new Vector2(60f, 30f);
            rt.localScale = Vector3.one;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 28f;
            tmp.alignment = TextAlignmentOptions.BottomRight;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = Color.black;
            tmp.raycastTarget = false;
            tmp.text = string.Empty;

            // Asegurar que se renderiza encima de los overlays — última posición sibling.
            go.transform.SetAsLastSibling();
            return tmp;
        }

        /// <summary>
        /// Garantiza que el slot tenga un <see cref="Button"/> auto-resoluble en
        /// <see cref="OnEnable"/>. Si el GameObject no tiene Button/Image (ej. setup
        /// pre-existente sin botón cableado), se agregan en runtime con un Image
        /// casi transparente que sirve de raycast target.
        /// </summary>
        private void EnsureClickable()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null) return;

            // Sin Image en el root del slot, el Button no recibe clicks. Agregamos
            // uno transparente como raycast target.
            var img = GetComponent<Image>();
            if (img == null)
            {
                img = gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.01f);
                img.raycastTarget = true;
            }

            _button = gameObject.AddComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button == null) _button = GetComponent<Button>();
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

        /// <summary>
        /// Actualiza el label de cantidad. Si <paramref name="count"/> &lt;=
        /// <see cref="_hideCountAtOrBelow"/>, esconde el label.
        /// </summary>
        public void SetCount(int count)
        {
            if (_countLabel == null) return;

            if (count <= _hideCountAtOrBelow)
            {
                _countLabel.gameObject.SetActive(false);
            }
            else
            {
                _countLabel.gameObject.SetActive(true);
                _countLabel.text = string.Format(_countLabelFormat, count);
            }
        }
    }
}
