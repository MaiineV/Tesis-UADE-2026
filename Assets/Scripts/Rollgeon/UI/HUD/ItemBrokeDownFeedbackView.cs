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
    /// Feedback de un item que se rompe (Eco Menguante al llegar a x1.0): toast "¡Se rompió!"
    /// + nombre del item sobre la pila de vida y un punch de escala. Sin esto el item
    /// desaparece en silencio y el jugador no entiende por qué su daño volvió a la normalidad.
    /// </summary>
    /// <remarks>
    /// Hermano de <see cref="SecondWindFeedbackView"/>: vive junto a
    /// <see cref="HealthChipStackView"/> en Canvas_PlayerStatus y escucha
    /// <see cref="EventName.OnItemBrokeDown"/> en OnEnable/OnDisable.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Item Broke Down Feedback View")]
    public sealed class ItemBrokeDownFeedbackView : MonoBehaviour
    {
        [Title("Item Broke Down — Toast")]
        [SerializeField, Tooltip("Rect sobre el que aparece el toast y que recibe el punch. Null = este objeto.")]
        private RectTransform _anchor;

        [SerializeField, Tooltip("Fuente del toast. Null = la del primer TMP hijo (label hp/max), o default de TMP.")]
        private TMP_FontAsset _font;

        [SerializeField]
        private Color _color = new Color(0.85f, 0.55f, 0.95f);

        [Title("Item Broke Down — Punch")]
        [SerializeField, Range(0f, 1f), Tooltip("Punch de escala de la pila de vida (0 = sin punch).")]
        private float _punch = 0.15f;

        [SerializeField, MinValue(0.05f)]
        private float _punchSeconds = 0.35f;

        private EventManager.EventReceiver _handler;

        private void OnEnable()
        {
            if (_handler != null) return;
            _handler = HandleItemBrokeDown;
            EventManager.Subscribe(EventName.OnItemBrokeDown, _handler);
        }

        private void OnDisable()
        {
            if (_handler == null) return;
            EventManager.UnSubscribe(EventName.OnItemBrokeDown, _handler);
            _handler = null;
        }

        private void HandleItemBrokeDown(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            Show(args[1] as ItemSO);
        }

        /// <summary>Muestra el toast y el punch. Público para smoke desde el editor.</summary>
        public void Show(ItemSO item)
        {
            var anchor = _anchor != null ? _anchor : transform as RectTransform;
            if (anchor == null) return;

            string itemName = item != null ? BreakdownIconResolver.ResolveDisplayName(item) : string.Empty;
            string title = LocalizedContent.Ui(UiTextKeys.ItemBrokeDownTitle, "¡Se rompió!");
            string body = string.Format(
                LocalizedContent.Ui(UiTextKeys.ItemBrokeDownBody, "{0} agotó su poder y desapareció."),
                itemName);

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
