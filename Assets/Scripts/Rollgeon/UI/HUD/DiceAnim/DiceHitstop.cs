using System.Collections;
using Rollgeon.Timing;
using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Hitstop de UI: congela <c>Time.timeScale</c> por unos pocos frames para
    /// puntuar un momento (crit, aterrizaje del throw). Seguro en un juego por
    /// turnos — nada depende de tiempo real durante la pausa. Usa un runner
    /// propio con <c>WaitForSecondsRealtime</c> (los tweens/coroutines scaled
    /// quedan pausados, que es exactamente el efecto buscado).
    /// Restaura leyendo <see cref="GameSpeedPrefs"/> — la única fuente de verdad
    /// del timeScale en reposo — así un cambio de speed durante el freeze no se
    /// pierde al descongelar.
    /// </summary>
    public static class DiceHitstop
    {
        private static Runner _runner;
        private static bool _frozen;

        public static void Play(float seconds)
        {
            // El freeze es realtime: sin este ajuste, a x8 los 0.06s valdrían
            // casi medio segundo de tiempo de juego — un stall, no un acento.
            seconds /= GameSpeedPrefs.Multiplier;
            if (seconds <= 0f || !Application.isPlaying) return;
            if (_runner == null)
            {
                var go = new GameObject("~DiceHitstop");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                _runner = go.AddComponent<Runner>();
            }
            _frozen = true;
            Time.timeScale = 0f;
            _runner.Restart(seconds);
        }

        private static void Restore()
        {
            if (!_frozen) return;
            Time.timeScale = GameSpeedPrefs.Multiplier;
            _frozen = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _runner = null;
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
