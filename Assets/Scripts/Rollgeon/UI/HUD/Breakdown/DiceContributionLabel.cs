using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
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

        private Tween _appear;
        private bool _outlineApplied;

        /// <summary>
        /// Muestra el aporte total; <paramref name="bonusPortion"/> separa cuánto vino
        /// de encantamientos (0 = todo cara). <paramref name="appearDelay"/> se usa para
        /// el stagger de aparición (0 = inmediato).
        /// </summary>
        public void Show(int amount, int bonusPortion = 0, float appearDelay = 0f)
        {
            Amount = amount;
            if (_label != null) _label.text = BuildText(amount, bonusPortion);
            if (_appear.isAlive) _appear.Stop();
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            // Outline recién con el GO activo: tocar outlineWidth con el TMP dormido
            // (GO inactivo desde el prefab) tira NRE por material interno null.
            if (!_outlineApplied && _label != null)
            {
                _outlineApplied = true;
                _label.outlineWidth = 0.2f;
                // Outline oscuro de la paleta de la mesa (#0A0A0C) — mismo que ValueTextOutline.mat.
                _label.outlineColor = new Color32(0x0A, 0x0A, 0x0C, 0xFF);
            }

            // Stagger: aparece en cascada con pop OutBack. Cualquier Hide() lo cancela,
            // así el outro/clear nunca deja un label apareciendo tarde.
            if (appearDelay > 0f && Application.isPlaying && !DiceUiMotionPrefs.ReducedMotion)
            {
                transform.localScale = Vector3.zero;
                _appear = Tween.Scale(transform, 1f, 0.15f, Ease.OutBack, startDelay: appearDelay);
            }
        }

        public void Hide()
        {
            Amount = 0;
            if (_appear.isAlive) _appear.Stop();
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_appear.isAlive) _appear.Stop();
            transform.localScale = Vector3.one;
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
