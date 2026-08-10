using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// El "+N" bajo un dado: lo que ese dado realmente suma al combo (cara + bonos
    /// aditivos de sus encantamientos). Solo visible en dados contribuyentes.
    /// La cara va en blanco hueso y la porción de encantamiento en dorado, para que
    /// se lea qué parte es del dado y qué parte del build.
    /// </summary>
    public sealed class DiceContributionLabel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _label;

        [SerializeField]
        [Tooltip("Color de la cara plana del dado (paleta DamageDealt).")]
        private Color _faceColor = new Color32(0xF5, 0xEF, 0xE0, 0xFF);

        [SerializeField]
        [Tooltip("Color de la porción de bono de encantamientos (dorado = bonus).")]
        private Color _bonusColor = new Color32(0xFF, 0xD7, 0x5A, 0xFF);

        public RectTransform Anchor => (RectTransform)transform;

        public int Amount { get; private set; }

        private void Awake()
        {
            // Outline una sola vez: el label cae sobre el sprite del dado y sin borde
            // se pierde. Setearlo instancia el material — por eso no va en Show().
            if (_label == null) return;
            _label.outlineWidth = 0.2f;
            _label.outlineColor = Color.black;
        }

        /// <summary>
        /// Muestra el aporte total; <paramref name="bonusPortion"/> separa cuánto vino
        /// de encantamientos (0 = todo cara). <paramref name="appearDelay"/> se usa para
        /// el stagger de aparición (0 = inmediato).
        /// </summary>
        public void Show(int amount, int bonusPortion = 0, float appearDelay = 0f)
        {
            Amount = amount;
            if (_label != null) _label.text = BuildText(amount, bonusPortion);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            Amount = 0;
            gameObject.SetActive(false);
        }

        private string BuildText(int amount, int bonusPortion)
        {
            int face = amount - bonusPortion;
            string faceHex = ColorUtility.ToHtmlStringRGB(_faceColor);
            if (bonusPortion <= 0 || face < 0)
                return $"<color=#{faceHex}>+{amount}</color>";
            string bonusHex = ColorUtility.ToHtmlStringRGB(_bonusColor);
            return $"<color=#{faceHex}>+{face}</color><color=#{bonusHex}>+{bonusPortion}</color>";
        }
    }
}
