using System.Collections;
using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Hitstop de UI: congela <c>Time.timeScale</c> por unos pocos frames para
    /// puntuar un momento (crit, aterrizaje del throw). Seguro en un juego por
    /// turnos — nada depende de tiempo real durante la pausa. Usa un runner
    /// propio con <c>WaitForSecondsRealtime</c> (los tweens/coroutines scaled
    /// quedan pausados, que es exactamente el efecto buscado).
    /// </summary>
    public static class DiceHitstop
    {
        private static Runner _runner;
        private static float _originalScale = 1f;
        private static bool _frozen;

        public static void Play(float seconds)
        {
            if (seconds <= 0f || !Application.isPlaying) return;
            if (_runner == null)
            {
                var go = new GameObject("~DiceHitstop");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                _runner = go.AddComponent<Runner>();
            }
            if (!_frozen)
            {
                // Solo capturamos la escala original al entrar — un Play durante otro
                // hitstop extiende la pausa sin pisar el valor a restaurar.
                _originalScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                _frozen = true;
            }
            Time.timeScale = 0f;
            _runner.Restart(seconds);
        }

        private static void Restore()
        {
            if (!_frozen) return;
            Time.timeScale = _originalScale;
            _frozen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _runner = null;
            _originalScale = 1f;
            _frozen = false;
        }

        private sealed class Runner : MonoBehaviour
        {
            private Coroutine _pending;

            public void Restart(float seconds)
            {
                if (_pending != null) StopCoroutine(_pending);
                _pending = StartCoroutine(RestoreAfter(seconds));
            }

            private IEnumerator RestoreAfter(float seconds)
            {
                yield return new WaitForSecondsRealtime(seconds);
                _pending = null;
                Restore();
            }

            private void OnDisable()
            {
                // Teardown con hitstop en vuelo: nunca dejar el juego congelado.
                Restore();
            }
        }
    }
}
