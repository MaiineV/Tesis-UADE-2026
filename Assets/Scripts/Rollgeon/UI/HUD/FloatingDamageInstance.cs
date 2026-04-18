using System.Collections;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Prefab-backed MonoBehaviour de un numero flotante. Expone
    /// <see cref="Play(string, Color, Vector3)"/> — fade-in, rise, fade-out, auto-destroy.
    /// Plan §3.8.
    /// </summary>
    /// <remarks>
    /// Implementacion default con <c>IEnumerator</c> + <c>Lerp</c> — PrimeTween / DOTween
    /// no son dependency hard en este worktree. Si el proyecto adopta uno, se migran
    /// los lerps sin tocar la API publica.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Floating Damage Instance")]
    public class FloatingDamageInstance : MonoBehaviour
    {
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

        /// <summary>
        /// Arranca la animacion con el <paramref name="text"/>, <paramref name="tint"/> y
        /// la posicion screen <paramref name="screenPos"/> (ScreenSpace Overlay).
        /// </summary>
        public void Play(string text, Color tint, Vector3 screenPos)
        {
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

            StartCoroutine(Animate());
        }

        private IEnumerator Animate()
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 endPos = startPos + new Vector3(0f, _riseHeight, 0f);

            while (elapsed < _durationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _durationSeconds);

                transform.position = Vector3.Lerp(startPos, endPos, t);

                if (_canvasGroup != null)
                {
                    if (t < _fadeOutRatio)
                    {
                        _canvasGroup.alpha = 1f;
                    }
                    else
                    {
                        float fadeT = (t - _fadeOutRatio) / (1f - _fadeOutRatio);
                        _canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeT);
                    }
                }
                yield return null;
            }

            if (Application.isPlaying) Destroy(gameObject);
        }
    }
}
