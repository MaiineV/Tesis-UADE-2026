using System;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Cierra un telón de dos hojas sobre la pantalla y avisa al terminar. Es el
    /// inverso del intro del menú (<see cref="MainMenuIntroAnimation"/> las abre
    /// hacia los costados): acá entran desde afuera y se juntan en el centro.
    /// Lo usan <see cref="VictoryScreen"/> y <see cref="DefeatScreen"/> para tapar
    /// la escena antes de volver al menú principal.
    /// </summary>
    /// <remarks>
    /// [SETUP] Las hojas se autoran en su posición CERRADA (cubriendo la pantalla
    /// entre las dos) y quedan desactivadas hasta que <see cref="Play"/> las corre
    /// fuera de pantalla, las activa y las desliza de vuelta a la posición
    /// autorada. Wiring por "Rollgeon → Victory Defeat → Wire Curtain Close".
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Curtain Close Transition")]
    public class CurtainCloseTransition : MonoBehaviour
    {
        [Title("Telón (posición autorada = cerrado)")]
        [SerializeField] private RectTransform _curtainLeft;
        [SerializeField] private RectTransform _curtainRight;

        // Un poco más lento que la apertura del intro (1.2s): el cierre es una
        // despedida y se banca un beat extra; a la velocidad del intro se sentía brusco.
        [SerializeField] private float _slideDuration = 1.8f;
        [SerializeField] private Ease _ease = Ease.InOutSine;

        public bool IsPlaying { get; private set; }

        private float _closedLeftX;
        private float _closedRightX;
        private bool _closedCaptured;
        private int _pendingTweens;
        private Action _onClosed;

        /// <summary>
        /// Dispara el cierre y llama a <paramref name="onClosed"/> una única vez
        /// cuando las dos hojas llegaron al centro. Sin hojas cableadas (o fuera
        /// de Play Mode, ej. tests EditMode) el estado final se aplica en el acto
        /// y el callback es sincrónico. Re-entrante no: mientras corre, ignora
        /// nuevas llamadas.
        /// </summary>
        public void Play(Action onClosed)
        {
            if (IsPlaying) return;

            // La posición autorada ES el estado cerrado — se captura una sola vez
            // porque Play mueve las hojas y una re-lectura vería el estado corrido.
            if (!_closedCaptured)
            {
                if (_curtainLeft != null) _closedLeftX = _curtainLeft.anchoredPosition.x;
                if (_curtainRight != null) _closedRightX = _curtainRight.anchoredPosition.x;
                _closedCaptured = true;
            }

            IsPlaying = true;
            _onClosed = onClosed;
            _pendingTweens = 0;

            bool instant = !Application.isPlaying;
            StartSide(_curtainLeft, _closedLeftX, openSign: -1f, instant);
            StartSide(_curtainRight, _closedRightX, openSign: +1f, instant);

            if (_pendingTweens == 0)
                Finish();
        }

        private void StartSide(RectTransform curtain, float closedX, float openSign, bool instant)
        {
            if (curtain == null) return;

            // Arriba de todo dentro de la screen: el telón tapa título y botón
            // mientras se cierra.
            curtain.SetAsLastSibling();
            curtain.gameObject.SetActive(true);

            if (instant)
            {
                curtain.anchoredPosition = new Vector2(closedX, curtain.anchoredPosition.y);
                return;
            }

            var openX = closedX + openSign * curtain.rect.width;
            curtain.anchoredPosition = new Vector2(openX, curtain.anchoredPosition.y);
            _pendingTweens++;
            Tween.UIAnchoredPositionX(curtain, closedX, _slideDuration, _ease)
                .OnComplete(OnSideClosed);
        }

        private void OnSideClosed()
        {
            _pendingTweens--;
            if (_pendingTweens <= 0)
                Finish();
        }

        private void Finish()
        {
            IsPlaying = false;
            var callback = _onClosed;
            _onClosed = null;
            callback?.Invoke();
        }
    }
}
