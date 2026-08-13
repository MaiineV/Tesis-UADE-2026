using System;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Una fila del pool en <see cref="BuildSelectionScreen"/>: el sprite del
    /// dado (clickeable — el click izquierdo AGREGA a la bolsa, el derecho QUITA
    /// una copia), el nombre del tipo y el contador "X / max". Se atenúa cuando el
    /// tipo se agota o la bolsa está llena. Quitar también se puede clickeando el
    /// dado en la tira de la bolsa: los dos caminos van al mismo handler.
    /// </summary>
    /// <remarks>
    /// La screen orquesta la logica (no la fila). La fila solo muestra el tipo,
    /// el contador "X / max" y emite eventos de click.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Pool Offering Row")]
    public class PoolOfferingRow : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Optional] private TextMeshProUGUI _typeLabel;
        [SerializeField, Optional] private TextMeshProUGUI _countLabel;

        [SerializeField, Optional]
        [Tooltip("Button del dado — clickear el sprite agrega a la bolsa (mock).")]
        private Button _addButton;

        [SerializeField, Optional]
        [Tooltip("Obsoleto desde el rework del mock (quitar = click en la tira). " +
                 "Se conserva por compat con prefabs/tests viejos.")]
        private Button _removeButton;

        [Title("Refs — mock rework")]
        [SerializeField, Optional, Tooltip("Image del sprite del dado (hija del add button).")]
        private Image _dieImage;

        [SerializeField, Optional, Tooltip("Línea separadora inferior de la fila.")]
        private RectTransform _divider;

        [SerializeField, Optional, Tooltip("CanvasGroup del root — atenúa la fila agotada.")]
        private CanvasGroup _rowGroup;

        private const float ExhaustedAlpha = 0.4f;

        private PointerRightClickRelay _rightClickRelay;
        private int _currentCount;

        public DiceType Type { get; private set; }
        public int MaxInBag { get; private set; }

        public event Action<DiceType> OnAddRequested;
        public event Action<DiceType> OnRemoveRequested;

        public void Bind(DiceType type, int maxInBag) => Bind(type, maxInBag, dieSprite: null);

        public void Bind(DiceType type, int maxInBag, Sprite dieSprite)
        {
            Type = type;
            MaxInBag = maxInBag;

            if (_typeLabel != null) _typeLabel.text = type.ToString();
            if (_dieImage != null && dieSprite != null) _dieImage.sprite = dieSprite;
            if (_addButton != null)
            {
                _addButton.onClick.AddListener(HandleAddClicked);
                EnsureRightClickRelay();
            }
            if (_removeButton != null) _removeButton.onClick.AddListener(HandleRemoveClicked);

            Refresh(0, bagHasRoom: true);
        }

        public void Unbind()
        {
            if (_addButton != null) _addButton.onClick.RemoveListener(HandleAddClicked);
            if (_removeButton != null) _removeButton.onClick.RemoveListener(HandleRemoveClicked);
            if (_rightClickRelay != null) _rightClickRelay.OnRightClick = null;
            OnAddRequested = null;
            OnRemoveRequested = null;
        }

        // El relay va sobre el GameObject del Button, no sobre el root de la fila: es el
        // unico raycast target, y uGUI entrega el click al primer ancestro que maneja
        // IPointerClickHandler — que es el propio Button. Agregado por codigo para no
        // pedir una re-autoria del prefab.
        private void EnsureRightClickRelay()
        {
            if (_rightClickRelay == null)
                _rightClickRelay = _addButton.gameObject.GetComponent<PointerRightClickRelay>()
                                   ?? _addButton.gameObject.AddComponent<PointerRightClickRelay>();

            _rightClickRelay.OnRightClick = HandleRightClickRemove;
        }

        /// <summary>
        /// Refresca el contador y la interactividad. <paramref name="bagHasRoom"/>
        /// apaga el add cuando la bolsa ya alcanzo <c>RequiredBagSize</c>. La fila
        /// agotada (sin más copias disponibles) se atenúa como en el mock.
        /// </summary>
        public void Refresh(int currentCount, bool bagHasRoom)
        {
            _currentCount = currentCount;
            if (_countLabel != null) _countLabel.text = $"{currentCount} / {MaxInBag}";

            bool canAdd = bagHasRoom && currentCount < MaxInBag;
            if (_addButton != null) _addButton.interactable = canAdd;
            if (_removeButton != null) _removeButton.interactable = currentCount > 0;

            // Gris solo cuando el TIPO se agotó (mock: fila D20 1/1 en gris) — la
            // bolsa llena no atenúa las filas, solo apaga el click.
            if (_rowGroup != null)
            {
                _rowGroup.alpha = currentCount >= MaxInBag ? ExhaustedAlpha : 1f;
            }
        }

        /// <summary>El installer/screen apaga el divider de la última fila.</summary>
        public void SetDividerVisible(bool visible)
        {
            if (_divider != null) _divider.gameObject.SetActive(visible);
        }

        private void HandleAddClicked()
        {
            PunchDie();
            OnAddRequested?.Invoke(Type);
        }

        private void HandleRemoveClicked() => OnRemoveRequested?.Invoke(Type);

        /// <summary>
        /// Click derecho sobre el dado del pool: saca una copia de la bolsa, igual que
        /// clickear ese dado en la tira.
        /// </summary>
        /// <remarks>
        /// A diferencia del agregar, esto NO mira <c>_addButton.interactable</c>: el add
        /// se apaga justo cuando la bolsa está llena o el tipo se agotó, que es cuando
        /// más querés poder sacar. El único gate es tener algo de este tipo puesto.
        /// </remarks>
        private void HandleRightClickRemove()
        {
            if (_currentCount <= 0) return;
            PunchDie();
            OnRemoveRequested?.Invoke(Type);
        }

        private void PunchDie()
        {
            if (_dieImage == null || !Application.isPlaying) return;
            if (Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;

            var t = _dieImage.transform;
            PrimeTween.Tween.StopAll(onTarget: t);
            t.localScale = Vector3.one;
            PrimeTween.Tween.PunchScale(t, Vector3.one * 0.15f, 0.2f, frequency: 3, useUnscaledTime: true);
        }
    }
}
