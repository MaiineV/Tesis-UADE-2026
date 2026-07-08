using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Prefab-backed MonoBehaviour de un numero flotante. Expone
    /// <see cref="Play(string, Color, Vector3, float, FloatingMotion)"/> — pop-in, rise/arc,
    /// fade-out, auto-destroy. Plan §3.8 / feedback-numerico-combate.
    /// </summary>
    /// <remarks>
    /// Implementacion default con <c>IEnumerator</c> + easings a mano — PrimeTween / DOTween
    /// no son dependency hard en este worktree. Si el proyecto adopta uno, se migran
    /// los lerps sin tocar la API publica.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Floating Damage Instance")]
    public class FloatingDamageInstance : MonoBehaviour
    {
        // Duración fija del "punch" de escala inicial — independiente de _durationSeconds
        // para que el pop se sienta igual de rápido sin importar cuánto dure el rise.
        private const float PopInDurationSeconds = 0.18f;
        private const float PopInStartScale = 0.6f;

        // Drift lateral de Motion.Arc — deliberadamente chico (no tapa números vecinos
        // durante el stagger) pero visible a simple vista.
        private const float ArcLateralAmplitude = 42f;

        [Title("Floating Damage — Refs")]
        [Required("Arrastrar el TextMeshProUGUI que muestra el numero.")]
        [SerializeField]
        private TextMeshProUGUI _text;

        [Required("Arrastrar el CanvasGroup (para alpha animado).")]
        [SerializeField]
        private CanvasGroup _canvasGroup;

        [Title("Floating Damage — Config")]
        [MinValue(0f)]
        [SerializeField]
        [Tooltip("Pixeles que sube el numero en su ciclo de vida.")]
        private float _riseHeight = 50f;

        [MinValue(0.05f)]
        [SerializeField]
        [Tooltip("Duracion total del ciclo (sec).")]
        private float _durationSeconds = 1.2f;

        [Range(0f, 1f)]
        [SerializeField]
        [Tooltip("Fraccion de la duracion durante la que se hace fade-out. Ej: 0.6 = " +
                 "ultimo 40% del tiempo se desvanece.")]
        private float _fadeOutRatio = 0.6f;

        // Alterna el lado del drift lateral en Motion.Arc para que spawns consecutivos
        // (ej. varios drops de oro seguidos) no se apilen todos hacia el mismo lado.
        // Estático y determinístico a propósito — nada de Random acá.
        private static int _arcAlternator;

        /// <summary>
        /// Arranca la animacion con el <paramref name="text"/>, <paramref name="tint"/> y
        /// la posicion screen <paramref name="screenPos"/> (ScreenSpace Overlay).
        /// <paramref name="scaleMultiplier"/> escala el tamaño final del punch (ver
        /// <see cref="Rollgeon.UI.HUD.FloatingNumberFormat"/>); <paramref name="motion"/>
        /// elige la curva de movimiento.
        /// </summary>
        public void Play(string text, Color tint, Vector3 screenPos,
            float scaleMultiplier = 1f, FloatingMotion motion = FloatingMotion.Rise)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            // activeSelf no basta si el padre esta inactivo — coroutines requieren activeInHierarchy.
            if (!gameObject.activeInHierarchy)
                return;

            if (_text != null)
            {
                _text.text = text ?? string.Empty;
                _text.color = tint;
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            // Posicionar en screen space — el canvas padre se encarga de convertir.
            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.position = screenPos;
            }
            else
            {
                transform.position = screenPos;
            }

            transform.localScale = Vector3.one * (PopInStartScale * scaleMultiplier);

            float lateralSign = (_arcAlternator++ % 2 == 0) ? 1f : -1f;
            StartCoroutine(Animate(scaleMultiplier, motion, lateralSign));
        }

        private IEnumerator Animate(float scaleMultiplier, FloatingMotion motion, float lateralSign)
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + new Vector3(0f, _riseHeight, 0f);

            while (elapsed < _durationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _durationSeconds);

                // Pop-in: overshoot de escala en los primeros PopInDurationSeconds, después
                // queda fija en scaleMultiplier — dá un "punch" perceptible sin animar todo
                // el ciclo de vida del número.
                float popT = Mathf.Clamp01(elapsed / PopInDurationSeconds);
                float scale = Mathf.LerpUnclamped(PopInStartScale, 1f, EaseOutBack(popT)) * scaleMultiplier;
                transform.localScale = Vector3.one * scale;

                // Arc usa una curva de seno (empieza y termina "suave") en vez del ease-out
                // cúbico de Rise — se siente más como un salto que como una subida derecha.
                float verticalT = motion == FloatingMotion.Arc
                    ? Mathf.Sin(t * Mathf.PI * 0.5f)
                    : EaseOutCubic(t);
                Vector3 pos = Vector3.LerpUnclamped(startPos, endPos, verticalT);

                if (motion == FloatingMotion.Arc)
                {
                    // Sin(t·π) va 0→amplitud→0: el número "sale" hacia un lado y vuelve al
                    // centro al terminar, como un salto en arco.
                    float lateral = lateralSign * ArcLateralAmplitude * Mathf.Sin(t * Mathf.PI);
                    pos += new Vector3(lateral, 0f, 0f);
                }

                transform.position = pos;

                if (_canvasGroup != null)
                {
                    if (t < _fadeOutRatio)
                    {
                        _canvasGroup.alpha = 1f;
                    }
                    else
                    {
                        float fadeT = (t - _fadeOutRatio) / (1f - _fadeOutRatio);
                        _canvasGroup.alpha = 1f - EaseInQuad(fadeT);
                    }
                }
                yield return null;
            }

            if (Application.isPlaying) Destroy(gameObject);
        }

        // ======================================================================
        // Easings — testeables (FloatingNumberFormatTests).
        // ======================================================================
        // Public y no internal: el assembly "Rollgeon" no tiene
        // InternalsVisibleTo("Rollgeon.UI.Tests") (ver InteractionPromptView.ResetForTests
        // para el mismo precedente) y agregarlo excede el alcance de este cambio.

        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float f = t - 1f;
            return f * f * f + 1f;
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = Mathf.Clamp01(t);
            float f = t - 1f;
            return 1f + c3 * f * f * f + c1 * f * f;
        }

        public static float EaseInQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }
    }
}
