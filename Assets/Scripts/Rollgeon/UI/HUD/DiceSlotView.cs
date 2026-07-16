using TMPro;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view for a single die slot. Used in build selection (Bind string) and
    /// in combat (ShowFace / SetHeld / OnToggled). UI#0013a / T97c.
    /// </summary>
    public class DiceSlotView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _diceLabel;

        [Title("Combat — Hold toggle")]
        [SerializeField, Optional] private Button _button;

        [Tooltip("Cuerpo del dado: lleva la silueta del tipo y recibe los tints de hold/blocked.")]
        [SerializeField, Optional] private Image _background;

        [Title("Forma por tipo de dado")]
        [SerializeField, Optional]
        [Tooltip("Catálogo de siluetas. Si queda vacío se resuelve desde " +
                 "Resources/Dice/DiceShapeCatalog.")]
        private DiceShapeCatalogSO _shapeCatalog;

        [Title("Dice block")]
        [SerializeField, Optional]
        [Tooltip("Ícono de candado que se muestra cuando el dado está bloqueado. Opcional.")]
        private GameObject _lockIcon;

        [HideInInspector] public UnityEvent OnToggled = new UnityEvent();

        private static readonly Color BlockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        private Color _defaultColor;
        private bool _blocked;
        private bool _held;

        private DiceShapeCatalogSO _resolvedCatalog;
        private bool _catalogResolved;

        private void Awake()
        {
            if (_background != null) _defaultColor = _background.color;
            if (_button != null) _button.onClick.AddListener(() => OnToggled.Invoke());
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveAllListeners();
        }

        /// <summary>Build selection — set die type label.</summary>
        public void Bind(string diceTypeName)
        {
            if (_diceLabel != null)
                _diceLabel.text = diceTypeName;
        }

        /// <summary>Build selection — etiqueta y silueta del tipo de dado.</summary>
        public void Bind(DiceType type)
        {
            Bind(type.ToString());
            SetDiceType(type);
        }

        /// <summary>
        /// Pinta la silueta del tipo de dado; el número TMP se dibuja encima. Sin catálogo o sin
        /// entrada, el sprite queda en null y el <see cref="Image"/> dibuja el cuadrado de color
        /// plano de siempre — por eso esto es seguro antes de que exista el arte.
        /// </summary>
        public void SetDiceType(DiceType type)
        {
            if (_background == null) return;
            var catalog = ResolveCatalog();
            _background.sprite = catalog != null ? catalog.GetShape(type) : null;
        }

        private DiceShapeCatalogSO ResolveCatalog()
        {
            if (_catalogResolved) return _resolvedCatalog;
            _resolvedCatalog = DiceShapeCatalogSO.Resolve(_shapeCatalog);
            _catalogResolved = true;
            return _resolvedCatalog;
        }

        /// <summary>Última cara mostrada (0 = sin tirada). Lo lee el presenter de
        /// throw para etiquetar el ghost al agarrar este dado en un grab-reroll.</summary>
        public int CurrentFace { get; private set; }

        /// <summary>Combat — show rolled face value.</summary>
        public void ShowFace(int face)
        {
            CurrentFace = face;
            _diceLabel?.SetText(face.ToString());
        }

        /// <summary>
        /// Cara transitoria durante el spin del roll. Solo pinta el label — no toca
        /// <see cref="CurrentFace"/>, que debe seguir reflejando la última cara real
        /// (la lee el presenter de throw para el ghost del grab-reroll).
        /// </summary>
        public void SetSpinPreviewFace(int face)
        {
            _diceLabel?.SetText(face.ToString());
        }

        /// <summary>
        /// Habilita/deshabilita el toggle de hold sin pisar el estado de bloqueo
        /// (un dado bloqueado por boss queda no-interactable aunque pidan true).
        /// Lo usa la animación de spin: un dado que todavía rueda no se puede holdear.
        /// </summary>
        public void SetHoldInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable && !_blocked;
        }

        /// <summary>Combat — toggle held visual (blue tint). Sin efecto si el dado está bloqueado.</summary>
        public void SetHeld(bool held)
        {
            _held = held;
            if (_blocked) return; // El estado bloqueado pisa el tint de hold.
            if (_background == null) return;
            _background.color = held ? new Color(0.4f, 0.8f, 1f, 1f) : _defaultColor;
        }

        /// <summary>
        /// Boss 1 (§2) — marca el dado como bloqueado: grayed-out + ícono de candado, y desactiva
        /// el botón de hold. Al desbloquear, restaura el botón y el tint según el estado de hold.
        /// </summary>
        public void SetBlocked(bool blocked)
        {
            _blocked = blocked;
            if (_lockIcon != null) _lockIcon.SetActive(blocked);
            if (_button != null) _button.interactable = !blocked;
            if (_background != null)
                _background.color = blocked
                    ? BlockedColor
                    : (_held ? new Color(0.4f, 0.8f, 1f, 1f) : _defaultColor);
        }

        /// <summary>
        /// Combat — apaga el slot: limpia el label y quita el tint de hold.
        /// Lo invoca <see cref="DiceZoneView"/> al iniciar y al cerrar el turno.
        /// </summary>
        public void Clear()
        {
            CurrentFace = 0;
            _diceLabel?.SetText(string.Empty);
            SetBlocked(false);
            SetHeld(false);
        }
    }
}
