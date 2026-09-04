using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// El candado del fijado de tooltip, animado: al fijar aparece con el arco abierto
    /// y SE CIERRA (el gesto inverso al candado de forzar puerta, en miniatura); al
    /// soltar, el arco se abre y recién ahí desaparece. Misma receta procedural que
    /// <c>BreakdownSequenceDirector.OpenShackle</c> — subir + inclinar el arco — pero
    /// a escala del indicador de 22px.
    /// </summary>
    /// <remarks>
    /// Sin <see cref="_shackle"/> cableado (o con ReducedMotion) degrada al
    /// SetActive de siempre. <c>SetPinned</c> es idempotente por estado: los re-Show
    /// del panel re-afirman el pin y no deben re-disparar el cierre.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Tooltip Pin Lock View")]
    public sealed class TooltipPinLockView : MonoBehaviour
    {
        [Tooltip("El arco del candado. Null = sin animación, solo SetActive.")]
        [SerializeField] private RectTransform _shackle;

        [Tooltip("Cuánto sube el arco abierto (px del canvas, escala del indicador chico).")]
        [SerializeField] private float _liftPixels = 4.5f;

        [Tooltip("Inclinación del arco abierto (grados; negativo = hacia la derecha).")]
        [SerializeField] private float _tiltDegrees = -22f;

        [SerializeField] private float _closeDuration = 0.16f;
        [SerializeField] private float _openDuration = 0.14f;

        private Vector2 _home;
        private Quaternion _homeRotation;
        private bool _homeCached;
        private bool _pinned;

        // La escala autorada del indicador (el installer lo deja en 1.5): los resets
        // defensivos vuelven ACÁ, no a one — mismo criterio que WorldSpaceHealthBar.
        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        /// <summary>
        /// Apaga el candado YA, sin animación y sin tocar el estado lógico del pin.
        /// Es el reset que corre cada <c>Show</c> del panel compartido: la tooltip de
        /// otra cosa no debe mostrar el candado del fijado (ni reproducir su apertura
        /// — el bug del "se desbloquea" al hoverear otro enemigo). El dueño del pin lo
        /// re-afirma en SU Show y el candado reaparece cerrado sin re-animar.
        /// </summary>
        public void HideImmediate()
        {
            StopTweens();
            RestoreHome();
            transform.localScale = _baseScale;
            gameObject.SetActive(false);
        }

        public void SetPinned(bool pinned)
        {
            if (_shackle == null || DiceUiMotionPrefs.ReducedMotion)
            {
                _pinned = pinned;
                gameObject.SetActive(pinned);
                return;
            }

            if (pinned)
            {
                // Ya fijado y visible: no-op (los refresh del panel re-afirman seguido).
                if (_pinned && gameObject.activeSelf) return;

                // Solo el flanco false→true anima el cierre; la re-afirmación tras un
                // HideImmediate (el pin volvió a mostrarse) aparece cerrado directo.
                bool freshlyPinned = !_pinned;
                _pinned = true;
                if (freshlyPinned) PlayClose();
                else ShowClosedImmediate();
            }
            else
            {
                if (!_pinned)
                {
                    // Estado ya suelto pero visual colgado (tween interrumpido por un
                    // cierre del panel): limpiar — el bug del candado en TODAS las tooltips.
                    if (gameObject.activeSelf) HideImmediate();
                    return;
                }

                _pinned = false;
                if (gameObject.activeInHierarchy) PlayOpen();
                else HideImmediate();
            }
        }

        private void ShowClosedImmediate()
        {
            gameObject.SetActive(true);
            CacheHome();
            StopTweens();
            RestoreHome();
            transform.localScale = _baseScale;
        }

        private void PlayClose()
        {
            gameObject.SetActive(true);
            CacheHome();
            StopTweens();

            // Sin punch de escala: interrumpido a mitad (los re-Show del panel pisan
            // el pin seguido) dejaba el candado agrandado. Solo el arco se mueve.
            transform.localScale = _baseScale;

            // Arranca abierto y baja a cerrado: el "clic" de fijar.
            _shackle.anchoredPosition = OpenPosition();
            _shackle.localRotation = OpenRotation();
            Tween.UIAnchoredPosition(_shackle, _home, _closeDuration, Ease.InQuad);
            Tween.LocalRotation(_shackle, _homeRotation, _closeDuration, Ease.InQuad);
        }

        private void PlayOpen()
        {
            // Panel ya oculto (o nunca mostrado): no hay nada que animar.
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            CacheHome();
            StopTweens();
            transform.localScale = _baseScale;
            Tween.UIAnchoredPosition(_shackle, OpenPosition(), _openDuration, Ease.OutBack);
            Tween.LocalRotation(_shackle, OpenRotation(), _openDuration, Ease.OutBack)
                .OnComplete(this, self => self.FinishOpen());
        }

        private void FinishOpen()
        {
            RestoreHome();
            gameObject.SetActive(false);
        }

        // Mismo desplazamiento que el candado grande: sube y se corre un cuarto del
        // lift hacia el costado, con la inclinación del arco.
        private Vector2 OpenPosition() => _home + new Vector2(_liftPixels * 0.25f, _liftPixels);
        private Quaternion OpenRotation() => _homeRotation * Quaternion.Euler(0f, 0f, _tiltDegrees);

        private void CacheHome()
        {
            if (_homeCached || _shackle == null) return;
            _home = _shackle.anchoredPosition;
            _homeRotation = _shackle.localRotation;
            _homeCached = true;
        }

        private void RestoreHome()
        {
            if (!_homeCached || _shackle == null) return;
            _shackle.anchoredPosition = _home;
            _shackle.localRotation = _homeRotation;
        }

        private void StopTweens()
        {
            if (_shackle != null) Tween.StopAll(onTarget: _shackle);
            Tween.StopAll(onTarget: transform);
        }

        // El panel puede apagarse a mitad de un tween (hover que se va, cambio de
        // sala): reposo limpio para la próxima aparición.
        private void OnDisable()
        {
            StopTweens();
            RestoreHome();
            transform.localScale = _baseScale;
        }
    }
}
