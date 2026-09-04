using System;
using System.Collections;
using Patterns;
using PrimeTween;
using Rollgeon.Dungeon;
using Rollgeon.Localization;
using Rollgeon.Timing;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Toast de sala (Feature#0086): al cruzar una puerta sube desde el borde inferior
    /// un panel con el tipo de sala (título) y su nombre (cuerpo), se sostiene un
    /// momento y baja solo. Acompaña el paneo de cámara entre salas.
    /// </summary>
    /// <remarks>
    /// [SETUP] Vive como hijo SIEMPRE ACTIVO de <c>Canvas_Toast/ToastCanvas</c>, al
    /// lado de <c>UnlockToastView</c>; solo <see cref="_panelRoot"/> se activa/desactiva.
    /// El panel ancla abajo-centro con pivot (0.5, 0): "oculto" es Y negativa (fuera
    /// de pantalla), "visible" es Y = margen inferior.
    /// Escucha <see cref="EventName.OnRoomCrossed"/>, no <c>OnRoomEntered</c>: la
    /// primera sala del piso y el resume de un save no anuncian nada.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Room Enter Toast View")]
    public sealed class RoomEnterToastView : MonoBehaviour
    {
        [Title("Room Toast")]
        [Required("Arrastrar el RectTransform raíz del panel (arranca inactivo, anclado abajo-centro).")]
        [SerializeField] private RectTransform _panelRoot;

        [Required("CanvasGroup del panel — el fade acompaña al slide.")]
        [SerializeField] private CanvasGroup _canvasGroup;

        [Required("TMP del tipo de sala (ej. 'Combate').")]
        [SerializeField] private TextMeshProUGUI _titleLabel;

        [Required("TMP del nombre de la sala.")]
        [SerializeField] private TextMeshProUGUI _bodyLabel;

        [Title("Timing")]
        [Tooltip("Segundos del slide de entrada (animación, no se divide por la velocidad de juego).")]
        [MinValue(0f)]
        [SerializeField] private float _slideInSeconds = 0.25f;

        [Tooltip("Segundos que el toast queda quieto. Pacing: se divide por GameSpeedPrefs.")]
        [MinValue(0f)]
        [SerializeField] private float _holdSeconds = 1.2f;

        [Tooltip("Segundos del slide de salida.")]
        [MinValue(0f)]
        [SerializeField] private float _slideOutSeconds = 0.2f;

        [Tooltip("Distancia del panel al borde inferior cuando está visible, en px de canvas.")]
        [SerializeField] private float _bottomMarginPx = 24f;

        private Tween _moveTween;
        private Tween _fadeTween;
        private Coroutine _cycle;
        private EventManager.EventReceiver _onRoomCrossedHandler;

        private float ShownY => _bottomMarginPx;
        private float HiddenY => -(_panelRoot != null ? _panelRoot.rect.height : 0f) - _bottomMarginPx;

        private void OnEnable()
        {
            _onRoomCrossedHandler ??= OnRoomCrossed;
            EventManager.Subscribe(EventName.OnRoomCrossed, _onRoomCrossedHandler);
            HideImmediate();
        }

        private void OnDisable()
        {
            if (_onRoomCrossedHandler != null)
                EventManager.UnSubscribe(EventName.OnRoomCrossed, _onRoomCrossedHandler);
            HideImmediate();
        }

        private void OnRoomCrossed(params object[] _)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return;
            var room = dungeon.CurrentRoom;
            if (room == null) return;

            Show(RoomTypeText.Localized(room.Type), LocalizedContent.Name(room.RoomId, room.DisplayName));
        }

        /// <summary>
        /// Muestra el toast con los textos dados. Un cruce mientras sigue visible corta el
        /// ciclo en curso y reinicia desde la posición actual, sin salto. Public para que
        /// un dev command o un test lo dispare sin pasar por el evento.
        /// </summary>
        public void Show(string title, string body)
        {
            if (_panelRoot == null) return;

            if (_titleLabel != null) _titleLabel.text = title;
            if (_bodyLabel != null) _bodyLabel.text = body;

            KillCycle();
            _panelRoot.gameObject.SetActive(true);
            _cycle = StartCoroutine(RunCycle());
        }

        private IEnumerator RunCycle()
        {
            float hold = _holdSeconds / Mathf.Max(1, GameSpeedPrefs.Multiplier);
            bool animate = Application.isPlaying && !DiceUiMotionPrefs.ReducedMotion;

            if (!animate)
            {
                SetY(ShownY);
                if (_canvasGroup != null) _canvasGroup.alpha = 1f;
                yield return new WaitForSeconds(hold);
                HideImmediate();
                _cycle = null;
                yield break;
            }

            // Slide-in desde donde esté (primera vez: fuera de pantalla).
            _moveTween = Tween.UIAnchoredPositionY(_panelRoot, ShownY, _slideInSeconds, Ease.OutCubic,
                useUnscaledTime: true);
            if (_canvasGroup != null)
                _fadeTween = Tween.Alpha(_canvasGroup, 1f, _slideInSeconds * 0.6f, Ease.OutQuad,
                    useUnscaledTime: true);
            yield return _moveTween.ToYieldInstruction();

            yield return new WaitForSecondsRealtime(hold);

            _moveTween = Tween.UIAnchoredPositionY(_panelRoot, HiddenY, _slideOutSeconds, Ease.InCubic,
                useUnscaledTime: true);
            if (_canvasGroup != null)
                _fadeTween = Tween.Alpha(_canvasGroup, 0f, _slideOutSeconds, Ease.InQuad,
                    startDelay: _slideOutSeconds * 0.3f, useUnscaledTime: true);
            yield return _moveTween.ToYieldInstruction();

            HideImmediate();
            _cycle = null;
        }

        private void KillCycle()
        {
            if (_cycle != null)
            {
                StopCoroutine(_cycle);
                _cycle = null;
            }
            if (_moveTween.isAlive) _moveTween.Stop();
            if (_fadeTween.isAlive) _fadeTween.Stop();
        }

        private void HideImmediate()
        {
            KillCycle();
            if (_panelRoot == null) return;
            SetY(HiddenY);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            _panelRoot.gameObject.SetActive(false);
        }

        private void SetY(float y)
        {
            var pos = _panelRoot.anchoredPosition;
            pos.y = y;
            _panelRoot.anchoredPosition = pos;
        }
    }
}
