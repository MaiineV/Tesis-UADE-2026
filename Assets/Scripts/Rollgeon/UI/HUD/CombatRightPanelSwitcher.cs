using Patterns;
using PrimeTween;
using Rollgeon.Input;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Alterna el panel superior-derecho del Combat HUD entre el carrusel de turnos
    /// (default) y el minimapa, con la tecla Tab (<see cref="GameplayHotkey.ToggleMinimap"/>).
    /// Animación: el panel saliente se desliza hacia la derecha con fade y el entrante
    /// entra desde la derecha (patrón <c>TurnQueueView.AnimateShift</c>).
    /// </summary>
    /// <remarks>
    /// Los paneles se ocultan por <see cref="CanvasGroup"/>, NUNCA por SetActive: el
    /// carrusel tiene que seguir procesando los eventos de turno mientras está oculto
    /// o vuelve desincronizado al togglear de vuelta.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Combat Right Panel Switcher")]
    public class CombatRightPanelSwitcher : MonoBehaviour
    {
        [Title("Paneles (mismo rect home — los cablea el installer)")]
        [SerializeField] private RectTransform _carouselPanel;
        [SerializeField] private CanvasGroup _carouselGroup;
        [SerializeField] private RectTransform _minimapPanel;
        [SerializeField] private CanvasGroup _minimapGroup;

        [Title("Slide")]
        [SerializeField, MinValue(0f)] private float _slideSeconds = 0.28f;
        [SerializeField, MinValue(0f)] private float _slideDistance = 220f;

        private IGameplayHotkeyService _hotkeys;
        private bool _showingMinimap;
        private bool _bound;
        private bool _homesCaptured;
        private Vector2 _carouselHome;
        private Vector2 _minimapHome;
        private Tween _outMove, _outFade, _inMove, _inFade;

        public bool ShowingMinimap => _showingMinimap;

        /// <summary>
        /// Se llama desde <c>CombatHUDView.BindAll</c>. Resetea SIEMPRE al carrusel con
        /// snap — re-entrar a combate arranca en el estado default, sin animación vieja.
        /// </summary>
        public void Bind()
        {
            if (_bound) Unbind();

            CaptureHomes();
            _showingMinimap = false;
            Snap();

            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out var hotkeys) && hotkeys != null)
            {
                _hotkeys = hotkeys;
                _hotkeys.Subscribe(GameplayHotkey.ToggleMinimap, OnToggleHotkey);
            }
            _bound = true;
        }

        public void Unbind()
        {
            if (!_bound) return;
            if (_hotkeys != null)
            {
                _hotkeys.Unsubscribe(GameplayHotkey.ToggleMinimap, OnToggleHotkey);
                _hotkeys = null;
            }
            CompleteTweens();
            _bound = false;
        }

        private void OnDisable()
        {
            // Un slide en vuelo cuando el HUD se apaga dejaría el panel a mitad de camino
            // y a PrimeTween tweeneando un target destruido en el teardown (gotcha
            // SlidingDrawer). Completar deja el estado final consistente.
            CompleteTweens();
        }

        private void OnToggleHotkey(InputAction.CallbackContext _) => Toggle();

        public void Toggle()
        {
            _showingMinimap = !_showingMinimap;

            CompleteTweens();

            bool instant = !Application.isPlaying
                           || DiceAnim.DiceUiMotionPrefs.ReducedMotion
                           || _slideSeconds <= 0f;
            if (instant)
            {
                Snap();
                return;
            }

            var (outRect, outGroup, outHome) = _showingMinimap
                ? (_carouselPanel, _carouselGroup, _carouselHome)
                : (_minimapPanel, _minimapGroup, _minimapHome);
            var (inRect, inGroup, inHome) = _showingMinimap
                ? (_minimapPanel, _minimapGroup, _minimapHome)
                : (_carouselPanel, _carouselGroup, _carouselHome);

            // Saliente: hacia la derecha con fade.
            if (outRect != null)
                _outMove = Tween.UIAnchoredPositionX(outRect, outHome.x + _slideDistance,
                    _slideSeconds, Ease.InCubic, useUnscaledTime: true);
            if (outGroup != null)
            {
                SetInteractive(outGroup, false);
                _outFade = Tween.Alpha(outGroup, 0f, _slideSeconds, Ease.InQuad, useUnscaledTime: true);
            }

            // Entrante: aparece corrido a la derecha y entra a su home.
            if (inRect != null)
            {
                inRect.anchoredPosition = new Vector2(inHome.x + _slideDistance, inHome.y);
                _inMove = Tween.UIAnchoredPositionX(inRect, inHome.x,
                    _slideSeconds, Ease.OutCubic, useUnscaledTime: true);
            }
            if (inGroup != null)
            {
                inGroup.alpha = 0f;
                SetInteractive(inGroup, true);
                _inFade = Tween.Alpha(inGroup, 1f, _slideSeconds, Ease.OutQuad, useUnscaledTime: true);
            }
        }

        // Homes capturados UNA sola vez (primer Bind) — el installer deja ambos paneles
        // en su rect del prefab, pero se lee del prefab real para no duplicar constantes.
        // Una re-captura en un re-bind posterior podría leer un panel corrido por un
        // slide anterior y corromper el home.
        private void CaptureHomes()
        {
            if (_homesCaptured) return;
            if (_carouselPanel != null) _carouselHome = _carouselPanel.anchoredPosition;
            if (_minimapPanel != null) _minimapHome = _minimapPanel.anchoredPosition;
            _homesCaptured = true;
        }

        private void Snap()
        {
            if (_carouselPanel != null) _carouselPanel.anchoredPosition = _carouselHome;
            if (_minimapPanel != null) _minimapPanel.anchoredPosition = _minimapHome;
            if (_carouselGroup != null)
            {
                _carouselGroup.alpha = _showingMinimap ? 0f : 1f;
                SetInteractive(_carouselGroup, !_showingMinimap);
            }
            if (_minimapGroup != null)
            {
                _minimapGroup.alpha = _showingMinimap ? 1f : 0f;
                SetInteractive(_minimapGroup, _showingMinimap);
            }
        }

        private static void SetInteractive(CanvasGroup group, bool on)
        {
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        private void CompleteTweens()
        {
            if (!Application.isPlaying) return; // PrimeTween no corre en EditMode/tests.
            if (_outMove.isAlive) _outMove.Complete();
            if (_outFade.isAlive) _outFade.Complete();
            if (_inMove.isAlive) _inMove.Complete();
            if (_inFade.isAlive) _inFade.Complete();
        }
    }
}
