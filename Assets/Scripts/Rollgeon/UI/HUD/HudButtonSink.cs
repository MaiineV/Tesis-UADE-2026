using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Hunde un <see cref="Button"/> del HUD hasta dejar media ficha visible sobre el
    /// borde inferior de la pantalla mientras esté no-interactable, y lo devuelve a su
    /// lugar al re-habilitarse — mismo lenguaje visual que la ficha usada de
    /// <see cref="ActionButton"/> y <see cref="ChipButtonVisual"/>, para los botones
    /// que ya tienen su propio manejo de sprites (<see cref="HudButtonSpriteSwap"/>)
    /// y solo necesitan el hundimiento.
    /// </summary>
    /// <remarks>
    /// Observa <see cref="Button.interactable"/> por polling en <c>LateUpdate</c>: los
    /// gates externos lo togglean desde varios caminos y Selectable no emite evento.
    /// Enforcement por frame (no tween one-shot): re-mide cada frame, así converge
    /// aunque otro sistema mueva el canvas entre medio. El regreso es a la
    /// anchoredPosition de origen, no por delta acumulado — las escrituras absolutas
    /// de otros sistemas desincronizaban el acumulado (ver ActionButton).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Hud Button Sink")]
    [RequireComponent(typeof(Button))]
    public sealed class HudButtonSink : MonoBehaviour
    {
        [SerializeField, MinValue(0f), Tooltip("Velocidad del hundimiento/regreso " +
                 "(px de pantalla por segundo). <= 0 = instantáneo.")]
        private float _sinkSpeed = 900f;

        private static readonly Vector3[] CornersScratch = new Vector3[4];

        private Button _button;
        private Vector2 _homeAnchoredPos;
        private bool _homeCaptured;
        private bool _needsSinkRestore;

        /// <summary>
        /// Attach idempotente por código — los prefabs existentes no traen el
        /// componente y las views lo agregan en su Awake (mismo patrón que el
        /// Outline de <see cref="ActionButton"/>).
        /// </summary>
        public static HudButtonSink Attach(Button button, float sinkSpeed = 900f)
        {
            if (button == null) return null;
            var sink = button.GetComponent<HudButtonSink>();
            if (sink == null) sink = button.gameObject.AddComponent<HudButtonSink>();
            sink._sinkSpeed = sinkSpeed;
            return sink;
        }

        private void LateUpdate()
        {
            if (_button == null) _button = GetComponent<Button>();
            var rect = transform as RectTransform;
            if (_button == null || rect == null) return;

            // Capturada en el primer frame (no en Awake): el layout inicial del HUD
            // puede acomodar el botón después de la instanciación.
            if (!_homeCaptured)
            {
                _homeAnchoredPos = rect.anchoredPosition;
                _homeCaptured = true;
            }

            bool instant = !Application.isPlaying || DiceUiMotionPrefs.ReducedMotion
                           || _sinkSpeed <= 0f;
            float step = instant ? float.MaxValue : _sinkSpeed * Time.unscaledDeltaTime;

            if (!_button.interactable)
            {
                _needsSinkRestore = true;
                float centerY = CurrentScreenCenterY(rect);
                if (centerY <= 0f) return; // ya está en/bajo el borde
                rect.position += Vector3.down * Mathf.Min(step, centerY);
            }
            else if (_needsSinkRestore)
            {
                rect.anchoredPosition = Vector2.MoveTowards(rect.anchoredPosition, _homeAnchoredPos, step);
                if (rect.anchoredPosition == _homeAnchoredPos) _needsSinkRestore = false;
            }
        }

        /// <summary>Centro vertical del rect en pantalla (ScreenSpaceOverlay: world == píxeles).</summary>
        private static float CurrentScreenCenterY(RectTransform rect)
        {
            rect.GetWorldCorners(CornersScratch);
            return (CornersScratch[0].y + CornersScratch[1].y) * 0.5f;
        }
    }
}
