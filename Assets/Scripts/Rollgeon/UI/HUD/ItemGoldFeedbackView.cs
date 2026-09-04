using System;
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
    /// Toast "Item: +X de oro" sobre la pila de oro cuando un ITEM le suma oro al jugador
    /// (<see cref="EventName.OnItemGoldGranted"/>: Bolsa del Impar, Tesoro de la Fortuna…), más
    /// un punch de escala de la pila. El floating "+XG" que ya emite <c>EffModifyGold</c> sale
    /// sobre el sprite del jugador y durante la tirada la mirada está en la mesa: la Bolsa
    /// cobraba y el jugador no se enteraba (playtest 2026-09-04).
    /// </summary>
    /// <remarks>
    /// Hermano de <see cref="ItemBrokeDownFeedbackView"/>: vive junto a
    /// <see cref="GoldChipStackView"/> en Canvas_PlayerStatus (GO <c>GoldStack</c>) y escucha el
    /// evento en OnEnable/OnDisable. El evento trae el <c>itemId</c> (no el SO) porque sale de
    /// un effect genérico; el nombre se resuelve por el inventario, con el id como último
    /// fallback.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Item Gold Feedback View")]
    public sealed class ItemGoldFeedbackView : MonoBehaviour
    {
        [Title("Item Gold — Toast")]
        [SerializeField, Tooltip("Rect sobre el que aparece el toast y que recibe el punch. Null = este objeto.")]
        private RectTransform _anchor;

        [SerializeField, Tooltip("Fuente del toast. Null = la del primer TMP hijo (label de oro), o default de TMP.")]
        private TMP_FontAsset _font;

        [SerializeField]
        private Color _color = new Color(0.95f, 0.78f, 0.30f);

        [Title("Item Gold — Punch")]
        [SerializeField, Range(0f, 1f), Tooltip("Punch de escala de la pila de oro (0 = sin punch).")]
        private float _punch = 0.12f;

        [SerializeField, MinValue(0.05f)]
        private float _punchSeconds = 0.3f;

        private EventManager.EventReceiver _handler;

        private void OnEnable()
        {
            if (_handler != null) return;
            _handler = HandleItemGoldGranted;
            EventManager.Subscribe(EventName.OnItemGoldGranted, _handler);
        }

        private void OnDisable()
        {
            if (_handler == null) return;
            EventManager.UnSubscribe(EventName.OnItemGoldGranted, _handler);
            _handler = null;
        }

        // Schema OnItemGoldGranted: [Guid playerGuid, string itemId, int amount]
        private void HandleItemGoldGranted(params object[] args)
        {
            if (args == null || args.Length < 3) return;
            if (!(args[2] is int amount) || amount <= 0) return;
            Show(args[1] as string, amount);
        }

        /// <summary>Muestra el toast y el punch. Público para smoke desde el editor.</summary>
        public void Show(string itemId, int amount)
        {
            var anchor = _anchor != null ? _anchor : transform as RectTransform;
            if (anchor == null) return;

            string body = string.Format(
                LocalizedContent.Ui(UiTextKeys.ItemGoldGrantedBody, "{0}: +{1} de oro"),
                ResolveItemName(itemId),
                amount);

            ActionRejectToast.Show(anchor, body, ResolveFont(), _color);

            if (_punch <= 0f || !Application.isPlaying || DiceAnim.DiceUiMotionPrefs.ReducedMotion) return;
            Tween.PunchScale(anchor, Vector3.one * _punch, _punchSeconds, frequency: 3);
        }

        private static string ResolveItemName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;

            if (ServiceLocator.TryGetService<IInventoryService>(out var inventory) && inventory != null)
            {
                var item = inventory.GetItem(itemId);
                if (item != null) return BreakdownIconResolver.ResolveDisplayName(item);
            }
            return LocalizedContent.Name(itemId, itemId);
        }

        private TMP_FontAsset ResolveFont()
        {
            if (_font != null) return _font;
            var tmp = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            return tmp != null ? tmp.font : null;
        }
    }
}
