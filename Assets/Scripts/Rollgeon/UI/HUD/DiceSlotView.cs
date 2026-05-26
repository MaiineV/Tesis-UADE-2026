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

        [HideInInspector] public UnityEvent OnToggled = new UnityEvent();

        private Color _defaultColor;

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

        /// <summary>Combat — toggle held visual (blue tint).</summary>
        public void SetHeld(bool held)
        {
            if (_background == null) return;
            _background.color = held ? new Color(0.4f, 0.8f, 1f, 1f) : _defaultColor;
        }

        /// <summary>
        /// Combat — apaga el slot: limpia el label y quita el tint de hold.
        /// Lo invoca <see cref="DiceZoneView"/> al iniciar y al cerrar el turno.
        /// </summary>
        public void Clear()
        {
            _diceLabel?.SetText(string.Empty);
            SetHeld(false);
        }
    }
}
