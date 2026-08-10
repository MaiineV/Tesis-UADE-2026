using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Flash de pantalla completa para el impacto del choque: una Image full-stretch
    /// (alpha 0 en reposo, raycast off) que sube rápido y baja suave. Corre en tiempo
    /// UNSCALED a propósito: dispara el mismo frame que el hitstop congela timeScale
    /// y tiene que verse durante el freeze.
    /// </summary>
    public sealed class ScreenFlashView : MonoBehaviour
    {
        [SerializeField] private Image _image;

        private Tween _tween;

        public void Flash(Color color, float peakAlpha, float seconds)
        {
            // Unscaled ⇒ el game speed no lo comprime solo: dividir acá para que
            // el flash no quede "largo" respecto de la secuencia acelerada.
            // (leer la pref, no Time.timeScale — puede estar en 0 por hitstop).
            seconds /= Rollgeon.Timing.GameSpeedPrefs.Multiplier;
            if (_image == null || peakAlpha <= 0f || seconds <= 0f) return;
            if (_tween.isAlive) _tween.Stop();

            var start = color;
            start.a = 0f;
            _image.color = start;
            _image.raycastTarget = false;

            // Pico al 25% del tiempo, resto es caída — más "golpe" que "fundido".
            _tween = Tween.Custom(this, 0f, 1f, seconds, (view, t) =>
            {
                float a = t < 0.25f
                    ? Mathf.Lerp(0f, peakAlpha, t / 0.25f)
                    : Mathf.Lerp(peakAlpha, 0f, (t - 0.25f) / 0.75f);
                var c = view._image.color;
                c.a = a;
                view._image.color = c;
            }, Ease.Linear, useUnscaledTime: true)
            .OnComplete(this, view => view.ResetAlpha());
        }

        private void ResetAlpha()
        {
            if (_image == null) return;
            var c = _image.color;
            c.a = 0f;
            _image.color = c;
        }

        private void OnDisable()
        {
            if (_tween.isAlive) _tween.Stop();
            ResetAlpha();
        }
    }
}
