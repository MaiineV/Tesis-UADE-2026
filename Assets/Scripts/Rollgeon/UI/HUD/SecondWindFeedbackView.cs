using Patterns;
using PrimeTween;
using Rollgeon.Items;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.Breakdown;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Feedback del Segundo Aliento: toast "¡Segundo Aliento!" + nombre del item consumido
    /// sobre la pila de vida, y un punch de escala de la pila. Sin esto el item se gasta
    /// en silencio (un log y un slot que desaparece) y el jugador no entiende por qué
    /// sigue vivo en 1 HP.
    /// </summary>
    /// <remarks>
    /// Vive junto a <see cref="HealthChipStackView"/> en Canvas_PlayerStatus (siempre
    /// activo, también en exploración). Escucha <see cref="EventName.OnSecondWindTriggered"/>
    /// en OnEnable/OnDisable — mismo ciclo de vida que las sub-views del HUD.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Second Wind Feedback View")]
    public sealed class SecondWindFeedbackView : MonoBehaviour
    {
        [Title("Second Wind — Toast")]
        [SerializeField, Tooltip("Rect sobre el que aparece el toast y que recibe el punch. Null = este objeto.")]
        private RectTransform _anchor;

        [SerializeField, Tooltip("Fuente del toast. Null = la del primer TMP hijo (label hp/max), o default de TMP.")]
        private TMP_FontAsset _font;

        [SerializeField]
        private Color _color = new Color(1f, 0.85f, 0.35f);

        [Title("Second Wind — Punch")]
        [SerializeField, Range(0f, 1f), Tooltip("Punch de escala de la pila de vida (0 = sin punch).")]
        private float _punch = 0.2f;

        [SerializeField, MinValue(0.05f)]
        private float _punchSeconds = 0.35f;

        private EventManager.EventReceiver _handler;

        private void OnEnable()
        {
            if (_handler != null) return;
            _handler = HandleSecondWind;
            EventManager.Subscribe(EventName.OnSecondWindTriggered, _handler);
        }

        private void OnDisable()
        {
            if (_handler == null) return;
            EventManager.UnSubscribe(EventName.OnSecondWindTriggered, _handler);
            _handler = null;
        }

        private void HandleSecondWind(params object[] args)
        {
            if (args == null || args.Length < 3) return;
            var item = args[1] as ItemSO;
            int remaining = args[2] is int hp ? hp : 1;
            Show(item, remaining);
        }

        /// <summary>Muestra el toast y el punch. Público para smoke desde el editor.</summary>
        public void Show(ItemSO item, int remainingHp)
        {
            var anchor = _anchor != null ? _anchor : transform as RectTransform;
            if (anchor == null) return;

            string itemName = item != null ? BreakdownIconResolver.ResolveDisplayName(item) : string.Empty;
            string title = LocalizedContent.Ui(UiTextKeys.SecondWindTitle, "¡Segundo Aliento!");
            string body = string.Format(
                LocalizedContent.Ui(UiTextKeys.SecondWindBody, "{0} te dejó en {1} HP."),
                itemName, remainingHp);

            ActionRejectToast.Show(anchor, title + "\n" + body, ResolveFont(), _color);

            if (_punch <= 0f || !Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            Tween.PunchScale(anchor, Vector3.one * _punch, _punchSeconds, frequency: 3);
        }

        private TMP_FontAsset ResolveFont()
        {
            if (_font != null) return _font;
            var tmp = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            return tmp != null ? tmp.font : null;
        }
    }
}
