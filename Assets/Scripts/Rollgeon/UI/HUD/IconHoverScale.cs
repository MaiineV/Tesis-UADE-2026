using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Agranda un ícono al pasarle el mouse y lo devuelve al salir — el feedback de "esto
    /// se puede clickear". Suelto y sin settings SO: son un puñado de íconos y los dos
    /// números que necesita entran en el Inspector.
    /// </summary>
    /// <remarks>
    /// MULTIPLICA la escala base en vez de setear una absoluta: la mochila y la bolsa de
    /// dados están autoradas a 0.8, y un target absoluto les cambiaría el tamaño de reposo
    /// en el primer hover.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Icon Hover Scale")]
    public class IconHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Range(1f, 1.5f)]
        [Tooltip("Multiplicador de la escala de reposo mientras el mouse está encima.")]
        private float _hoverScale = 1.12f;

        [SerializeField, Tooltip("Duración del crecimiento y de la vuelta.")]
        private float _duration = 0.12f;

        [SerializeField]
        [Tooltip("Opcional. Si está y no es interactable, el ícono no responde al hover.")]
        private Button _button;

        private Vector3 _baseScale = Vector3.one;
        private bool _baseScaleCaptured;

        private void Awake()
        {
            CaptureBaseScale();
            if (_button == null) TryGetComponent(out _button);
        }

        private void OnDisable()
        {
            // Un hover interrumpido por apagarse el HUD dejaría el ícono grande para siempre:
            // el pointer exit nunca llega si el objeto ya no está.
            if (_baseScaleCaptured) transform.localScale = _baseScale;
        }

        // Se captura una sola vez y nunca a mitad de tween, o el reposo se iría corriendo
        // hover tras hover.
        private void CaptureBaseScale()
        {
            if (_baseScaleCaptured) return;
            _baseScale = transform.localScale;
            _baseScaleCaptured = true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanJuice()) return;
            ScaleTo(_baseScale * _hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!CanJuice()) return;
            ScaleTo(_baseScale);
        }

        private bool CanJuice()
        {
            CaptureBaseScale();
            return Application.isPlaying
                   && !DiceUiMotionPrefs.ReducedMotion
                   && (_button == null || _button.interactable);
        }

        private void ScaleTo(Vector3 target)
        {
            Tween.StopAll(onTarget: transform);
            if (_duration <= 0f)
            {
                transform.localScale = target;
                return;
            }
            Tween.Scale(transform, target, _duration, Ease.OutQuad, useUnscaledTime: true);
        }
    }
}
