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

        [Tooltip("Si true, el slot se renderiza pero NO responde a clicks ni se " +
                 "auto-cablea un Button — display-only. Usado en Exploration HUD " +
                 "donde la pocion se consume via el boton Heal del ActionButtons, " +
                 "no via click directo en el slot.")]
        [SerializeField]
        private bool _displayOnly;

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

        [Tooltip("Sprite cuando hay al menos 1 del ítem (swap por cantidad, ej. " +
                 "PotionSheet_0). Si se cablea, gana sobre el swap por estado — " +
                 "ActiveItemsView llama SetCount después de SetState siempre.")]
        [SerializeField]
        private Sprite _iconWhenCountPositive;

        [Tooltip("Sprite cuando la cantidad es 0 (ej. PotionSheet_1). Opcional.")]
        [SerializeField]
        private Sprite _iconWhenCountZero;

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

        [Title("Cooldown (opcional)")]
        [Tooltip("Label TMP con los turnos que faltan para poder reusar el item. Si null, " +
                 "se auto-crea uno centrado en runtime la primera vez que hace falta.")]
        [SerializeField]
        private TextMeshProUGUI _cooldownLabel;

        [ShowInInspector, ReadOnly]
        public ActiveItemState CurrentState { get; private set; } = ActiveItemState.Inactive;

        /// <summary>Turnos restantes de cooldown. 0 = usable. Seam de test.</summary>
        [ShowInInspector, ReadOnly]
        public int CurrentCooldown { get; private set; }

        // Ortogonal al estado, espejo de ActionButton._affordable (BUG-074): con poción
        // en el inventario pero 0 rolls en el pool, el slot sigue Active pero tiene que
        // pintar el mismo rojo que los chips de acción.
        private bool _affordable = true;

        /// <summary>Estado actual de affordability — seam de test.</summary>
        public bool IsAffordableForTests => _affordable;

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
            // En modo display-only, el slot no debe ser clickeable — saltamos
            // tanto la auto-resolucion del Button como el AddComponent.
            if (_displayOnly) return;

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
            // Display-only: no resolvemos ni atamos eventos. El slot queda como
            // panel pasivo (icono + count + overlays).
            if (_displayOnly) return;

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
            // Inactive = el slot no representa ningun item (barra vacia): no hay nada que
            // rechazar. Depleted y Active-sin-recursos SI emiten, para que ActiveItemsView
            // resuelva el motivo y muestre el toast en vez de tragarse el tap.
            if (CurrentState == ActiveItemState.Inactive) return;
            OnClicked?.Invoke(this);
        }

        // El boton queda interactable mientras el slot tenga un item detras, aunque no se
        // pueda usar ahora — un boton no-interactable no dispara onClick y el jugador se
        // queda sin saber por que. El gate real vive en ActiveItemsView.HandleSlotClicked.
        private void RefreshInteractable()
        {
            if (_button != null)
            {
                _button.interactable = CurrentState != ActiveItemState.Inactive;
            }
        }

        /// <summary>
        /// Pisa el icono con el <c>ItemSO.Icon</c> del item que ocupa el slot. Lo usan los
        /// slots que <see cref="ActiveItemsView"/> instancia en runtime, donde el sprite no
        /// se puede cablear en Inspector porque el item se conoce recien en la run. En los
        /// slots pinneados (poción) no se llama: ahí mandan los sprites serializados.
        /// </summary>
        public void SetIcon(Sprite sprite)
        {
            if (_icon == null) return;
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
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

            RefreshUnavailableTint();
            RefreshInteractable();
        }

        /// <summary>
        /// Si al jugador le alcanzan los rolls para usar el ítem. Ortogonal a
        /// <see cref="SetState"/>: un slot Active con el pool vacío pinta el mismo
        /// rojo que los chips de acción (BUG-074).
        /// </summary>
        public void SetAffordable(bool affordable)
        {
            if (_affordable == affordable) return;
            _affordable = affordable;
            RefreshUnavailableTint();
        }

        // BUG-074: Inactive/Depleted = "no lo podés usar ahora", y Active-sin-rolls
        // también — mismo outline rojo que ActionButton.Unaffordable, para que la
        // ficha de ítem (poción/arco) conteste igual que los chips de acción.
        // Convive con los overlays de estado.
        private void RefreshUnavailableTint()
        {
            bool unavailable = CurrentState != ActiveItemState.Active || !_affordable;
            if (unavailable) UnavailableTint.Apply(_icon);
            else UnavailableTint.Remove(_icon);
        }

        /// <summary>
        /// Turnos que faltan para poder reusar el item. <c>0</c> esconde el label.
        /// El doc pide que el cooldown se lea como numero de turnos, no solo como tinte.
        /// </summary>
        public void SetCooldown(int turnsRemaining)
        {
            CurrentCooldown = Mathf.Max(0, turnsRemaining);

            if (_cooldownLabel == null)
            {
                // Sin cooldown activo no hace falta crear nada: la enorme mayoria de los
                // items tiene Cooldown 0 y nunca pasa por aca.
                if (CurrentCooldown <= 0) return;
                _cooldownLabel = BuildAutoCooldownLabel();
            }

            if (CurrentCooldown <= 0)
            {
                _cooldownLabel.gameObject.SetActive(false);
                return;
            }

            _cooldownLabel.gameObject.SetActive(true);
            _cooldownLabel.text = CurrentCooldown.ToString();
        }

        /// <summary>
        /// TMP centrado y grande sobre el icono — el numero de turnos tiene que leerse
        /// de un vistazo, a diferencia del contador de cargas que vive en la esquina.
        /// </summary>
        private TextMeshProUGUI BuildAutoCooldownLabel()
        {
            var go = new GameObject("CooldownLabel");
            go.transform.SetParent(transform, worldPositionStays: false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 44f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = Color.black;
            tmp.raycastTarget = false;
            tmp.text = string.Empty;

            go.transform.SetAsLastSibling();
            return tmp;
        }

        /// <summary>
        /// Actualiza el label de cantidad. Si <paramref name="count"/> &lt;=
        /// <see cref="_hideCountAtOrBelow"/>, esconde el label.
        /// </summary>
        public void SetCount(int count)
        {
            // Swap por cantidad ANTES del early-return del label: el sprite debe
            // actualizarse aunque el slot no tenga contador cableado.
            if (_icon != null && (_iconWhenCountPositive != null || _iconWhenCountZero != null))
            {
                var byCount = count >= 1 ? _iconWhenCountPositive : _iconWhenCountZero;
                if (byCount != null) _icon.sprite = byCount;
            }

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
