using TMPro;
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
        [SerializeField, Optional] private Graphic _background;

        [Title("Boss 1 — Dice block")]
        [SerializeField, Optional]
        [Tooltip("Ícono de candado que se muestra cuando el dado está bloqueado por el Boss. Opcional.")]
        private GameObject _lockIcon;

        [HideInInspector] public UnityEvent OnToggled = new UnityEvent();

        private static readonly Color BlockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        private Color _defaultColor;
        private bool _blocked;
        private bool _held;

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

        /// <summary>Combat — show rolled face value.</summary>
        public void ShowFace(int face) => _diceLabel?.SetText(face.ToString());

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
            _diceLabel?.SetText(string.Empty);
            SetBlocked(false);
            SetHeld(false);
        }
    }
}
