using System;
using Patterns;
using PrimeTween;
using Rollgeon.GameCamera;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Juice del boss bar (feedback de daño / heal / intro / muerte). Companion del
    /// <see cref="BossBarView"/> — vive en el mismo GameObject y el View le delega lo visual
    /// cuando está en Play. Sigue el idiom de <see cref="DiceBoardSkinJuice"/>: PrimeTween,
    /// refs <c>[Optional]</c> (sin wiring = no-op), captura de rest-pose y restore de tweens
    /// al ocultarse. Gateado por <c>DiceAnim.DiceUiMotionPrefs.ReducedMotion</c>.
    /// <para>
    /// El "chip damage" (doble barra) usa dos <see cref="Image"/> Filled: <see cref="_fillImage"/>
    /// (rojo, adelante, baja rápido) y <see cref="_ghostFillImage"/> (claro, atrás, baja con
    /// delay) — el hueco entre ambas es el daño recién recibido, que se cierra al alcanzar el
    /// ghost al fill.
    /// </para>
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Boss Bar Juice")]
    public sealed class BossBarJuice : MonoBehaviour
    {
        [Title("Targets (mismos del View)")]
        [SerializeField, Optional, Tooltip("Fill principal (rojo). Baja rápido y flashea al recibir daño.")]
        private Image _fillImage;

        [SerializeField, Optional, Tooltip("Fill fantasma detrás del principal. Baja con delay = chip de daño.")]
        private Image _ghostFillImage;

        [SerializeField, Optional, Tooltip("RectTransform a shakear (normalmente el Root de la barra).")]
        private RectTransform _shakeTarget;

        [SerializeField, Optional, Tooltip("Contador numérico — recibe punch al cambiar.")]
        private TextMeshProUGUI _hpText;

        [SerializeField, Optional, Tooltip("Nombre del jefe — recibe punch en la intro.")]
        private TextMeshProUGUI _nameText;

        [SerializeField, Optional, Tooltip("CanvasGroup del Root para fade de intro/muerte.")]
        private CanvasGroup _rootGroup;

        [SerializeField, Optional, Tooltip("Burst de partículas UI (reusa DiceThrowImpactBurst).")]
        private DiceThrowImpactBurst _hitBurst;

        [SerializeField, Optional, Tooltip("RectTransform del track — se usa su ancho para ubicar el burst en el borde del fill.")]
        private RectTransform _barTrack;

        [SerializeField, Optional, Tooltip("Borde de la barra — pulsa cuando la vida está baja.")]
        private Image _lifeBorder;

        [Title("Fill / chip")]
        [SerializeField, Tooltip("Duración de la bajada del fill principal.")]
        private float _fillDropDuration = 0.15f;

        [SerializeField, Tooltip("Duración de la bajada del ghost (más lenta).")]
        private float _ghostDropDuration = 0.45f;

        [SerializeField, Tooltip("Delay antes de que el ghost empiece a bajar.")]
        private float _ghostDropDelay = 0.25f;

        [SerializeField, Tooltip("Cuánto amplifica el drop de ratio a intensidad de juice (0.33 drop * 3 = full).")]
        private float _dropIntensityGain = 3f;

        [Title("Flash")]
        [SerializeField, Tooltip("Duración del flash de color del fill.")]
        private float _flashDuration = 0.18f;

        [SerializeField, Tooltip("Color del flash normal.")]
        private Color _flashColor = Color.white;

        [SerializeField, Tooltip("Color del flash en golpe a debilidad.")]
        private Color _weaknessColor = new Color(1f, 0.85f, 0.2f, 1f);

        [SerializeField, Tooltip("Color del flash de escudo (block / rompe-escudo).")]
        private Color _shieldColor = new Color(0.5f, 0.8f, 1f, 1f);

        [SerializeField, Tooltip("Color del flash de heal.")]
        private Color _healColor = new Color(0.4f, 1f, 0.5f, 1f);

        [Title("Shake")]
        [SerializeField, Tooltip("Shake (px) a intensidad 0.")]
        private float _shakeMinStrength = 6f;

        [SerializeField, Tooltip("Shake (px) a intensidad 1.")]
        private float _shakeMaxStrength = 26f;

        [SerializeField, Tooltip("Duración del shake.")]
        private float _shakeDuration = 0.28f;

        [SerializeField, Tooltip("Frecuencia del shake.")]
        private float _shakeFrequency = 22f;

        [SerializeField, Tooltip("Multiplicador de shake en golpe a debilidad.")]
        private float _weaknessShakeBoost = 1.5f;

        [SerializeField, Tooltip("Multiplicador de shake en golpe letal.")]
        private float _lethalShakeBoost = 2.2f;

        [Title("Counter punch")]
        [SerializeField, Tooltip("Fuerza del punch de escala del contador (0.35 = ±35%).")]
        private float _hpPunch = 0.35f;

        [SerializeField, Tooltip("Duración del punch del contador.")]
        private float _hpPunchDuration = 0.3f;

        [Title("Particles")]
        [SerializeField, Tooltip("Dirección del abanico de partículas.")]
        private Vector2 _burstDir = Vector2.up;

        [SerializeField, Range(0f, 1f), Tooltip("Piso de intensidad del burst (para que chip hits igual chispeen).")]
        private float _burstIntensityFloor = 0.25f;

        [Title("Screen shake / hitstop")]
        [SerializeField, Tooltip("Amplitud de shake de cámara a intensidad 0 (world units). 0 = sin shake de cámara.")]
        private float _camShakeMin = 0.12f;

        [SerializeField, Tooltip("Amplitud de shake de cámara a intensidad 1.")]
        private float _camShakeMax = 0.5f;

        [SerializeField, Tooltip("Duración del shake de cámara.")]
        private float _camShakeDuration = 0.22f;

        [SerializeField, Tooltip("Hitstop (freeze de timeScale, s) a intensidad mínima. 0 = sin hitstop.")]
        private float _hitstopMin = 0.03f;

        [SerializeField, Tooltip("Hitstop (s) a intensidad 1.")]
        private float _hitstopMax = 0.07f;

        [SerializeField, Range(0f, 1f), Tooltip("Intensidad mínima para que un golpe normal congele. Debajo, solo shake (chip hits no frizan).")]
        private float _hitstopMinIntensity = 0.15f;

        [SerializeField, Tooltip("Hitstop en golpe letal (más largo para puntuar la muerte).")]
        private float _lethalHitstop = 0.12f;

        [SerializeField, Tooltip("Amplitud de shake de cámara en la muerte del jefe.")]
        private float _deathCamShake = 0.6f;

        [SerializeField, Tooltip("Hitstop en la muerte del jefe.")]
        private float _deathHitstop = 0.12f;

        [Title("Intro / Death")]
        [SerializeField, Tooltip("Duración del pop de entrada.")]
        private float _introDuration = 0.35f;

        [SerializeField, Tooltip("Escala inicial del pop de entrada.")]
        private Vector3 _introFromScale = new Vector3(0.85f, 0.85f, 1f);

        [SerializeField, Tooltip("Punch del nombre en la intro.")]
        private float _namePunch = 0.4f;

        [SerializeField, Tooltip("Duración del fade-out de muerte.")]
        private float _deathDuration = 0.6f;

        [Title("Low HP pulse")]
        [SerializeField, Range(0f, 1f), Tooltip("Umbral de vida para arrancar el pulso.")]
        private float _lowHpThreshold = 0.25f;

        [SerializeField, Tooltip("Color pico del pulso de vida baja (sobre el borde).")]
        private Color _lowHpPulseColor = new Color(1f, 0.25f, 0.25f, 1f);

        [SerializeField, Tooltip("Período (ida y vuelta) del pulso de vida baja.")]
        private float _lowHpPulsePeriod = 0.7f;

        // Rest poses capturadas.
        // OJO: la del shake NO va acá. El Root está anclado arriba-centro, así que su
        // localPosition.y es medio alto del canvas — un valor que deriva el CanvasScaler y
        // que cambia con la resolución y con el primer layout pass. Cachearlo en OnEnable y
        // después escribirlo de vuelta reposicionaba la barra (con el canvas todavía sin
        // resolver quedaba en cero y la barra saltaba al centro de la pantalla). Se captura
        // fresco en cada shake, y sobre anchoredPosition, que es lo que el layout respeta.
        private Vector2 _shakeRestAnchored;
        private bool _shakeRestValid;
        private Vector3 _shakeRestScale = Vector3.one;
        private Vector3 _hpRestScale = Vector3.one;
        private Color _fillRestColor = Color.white;
        private Color _borderRestColor = Color.white;
        private float _rootRestAlpha = 1f;
        private bool _restCaptured;

        // Tweens vivos.
        private Tween _fillTween, _ghostTween, _flashTween, _shakeTween, _hpPunchTween, _introTween, _fadeTween, _lowHpTween;

        private static bool Active => Application.isPlaying;
        private static bool Motion => !DiceAnim.DiceUiMotionPrefs.ReducedMotion;

        private void OnEnable() => CaptureRest();

        private void OnDisable() => StopAndRestore();

        private void CaptureRest()
        {
            if (_restCaptured) return;
            if (_shakeTarget != null) _shakeRestScale = _shakeTarget.localScale;
            if (_hpText != null) _hpRestScale = _hpText.transform.localScale;
            if (_fillImage != null) _fillRestColor = _fillImage.color;
            if (_lifeBorder != null) _borderRestColor = _lifeBorder.color;
            if (_rootGroup != null) _rootRestAlpha = _rootGroup.alpha;
            _restCaptured = true;
        }

        /// <summary>Fija fill + ghost al ratio sin animar. Válido dentro y fuera de Play.</summary>
        public void SetImmediate(float ratio)
        {
            StopFillTweens();
            ratio = Mathf.Clamp01(ratio);
            if (_fillImage != null) _fillImage.fillAmount = ratio;
            if (_ghostFillImage != null) _ghostFillImage.fillAmount = ratio;
            UpdateLowHpPulse(ratio);
        }

        /// <summary>Pop de entrada: escala-in + fade + punch del nombre. Deja el fill en <paramref name="ratio"/>.</summary>
        public void PlayIntro(float ratio)
        {
            CaptureRest();
            SetImmediate(ratio);
            if (!Active) return;

            StopTransformTweens();
            if (Motion)
            {
                if (_shakeTarget != null)
                {
                    _shakeTarget.localScale = _introFromScale;
                    _introTween = Tween.Scale(_shakeTarget, _introFromScale, _shakeRestScale, _introDuration, Ease.OutBack);
                }
                if (_nameText != null)
                    Tween.PunchScale(_nameText.transform, Vector3.one * _namePunch, _introDuration, frequency: 3);
            }
            else if (_shakeTarget != null)
            {
                _shakeTarget.localScale = _shakeRestScale;
            }

            if (_rootGroup != null)
            {
                _rootGroup.alpha = 0f;
                _fadeTween = Tween.Alpha(_rootGroup, _rootRestAlpha, _introDuration, Ease.OutCubic);
            }
        }

        /// <summary>Daño: fill baja rápido, ghost con delay (chip), shake/flash/punch/partículas.</summary>
        public void PlayDamage(float from, float to, in DamageResolvedPayload p)
        {
            to = Mathf.Clamp01(to);
            if (!Active) { SetImmediate(to); return; }

            float intensity = Mathf.Clamp01(Mathf.Max(0f, from - to) * _dropIntensityGain);

            // Fill principal (rápido) + ghost (lento, con delay). Ambos desde su valor actual.
            StopFillTweens();
            if (!p.BlockedByShield && _fillImage != null)
                _fillTween = Tween.UIFillAmount(_fillImage, to, _fillDropDuration, Ease.OutCubic);
            if (!p.BlockedByShield && _ghostFillImage != null)
                _ghostTween = Tween.UIFillAmount(_ghostFillImage, to, _ghostDropDuration, Ease.InOutCubic, startDelay: _ghostDropDelay);

            // Flash de color (por tipo de golpe).
            Color flash = p.ShieldBroken || p.BlockedByShield ? _shieldColor
                        : p.WeaknessHit ? _weaknessColor
                        : _flashColor;
            Flash(flash);

            // Shake escalado + boosts.
            float mult = p.WasLethal ? _lethalShakeBoost : p.WeaknessHit ? _weaknessShakeBoost : 1f;
            float baseStrength = p.BlockedByShield ? _shakeMinStrength : Mathf.Lerp(_shakeMinStrength, _shakeMaxStrength, intensity);
            Shake(baseStrength * mult);

            // Screen shake (cámara) + hitstop. El shake de CameraService corre en unscaled
            // time, así que sigue sacudiendo DURANTE el freeze del hitstop — combo buscado.
            if (!p.BlockedByShield)
            {
                CameraShake(Mathf.Lerp(_camShakeMin, _camShakeMax, intensity) * mult);

                // Chip hits no frizan (sería molesto); sí los golpes fuertes o especiales.
                bool special = p.WeaknessHit || p.WasLethal || p.ShieldBroken;
                if (special || intensity >= _hitstopMinIntensity)
                    Hitstop(p.WasLethal ? _lethalHitstop : Mathf.Lerp(_hitstopMin, _hitstopMax, intensity));
            }

            // Punch del contador.
            if (Motion && _hpText != null)
            {
                if (_hpPunchTween.isAlive) _hpPunchTween.Stop();
                _hpPunchTween = Tween.PunchScale(_hpText.transform, Vector3.one * _hpPunch, _hpPunchDuration, frequency: 4);
            }

            // Partículas en el borde del fill.
            if (Motion && _hitBurst != null && !p.BlockedByShield)
            {
                float bi = Mathf.Max(_burstIntensityFloor, intensity);
                if (p.WeaknessHit || p.WasLethal) bi = Mathf.Min(1f, bi * 1.4f);
                _hitBurst.Burst(FillEdgeLocal(to), _burstDir, bi);
            }

            UpdateLowHpPulse(to);
        }

        /// <summary>Heal: fill/ghost suben animados + flash verde.</summary>
        public void PlayHeal(float from, float to)
        {
            to = Mathf.Clamp01(to);
            if (!Active) { SetImmediate(to); return; }

            StopFillTweens();
            if (_fillImage != null) _fillTween = Tween.UIFillAmount(_fillImage, to, _fillDropDuration, Ease.OutCubic);
            if (_ghostFillImage != null) _ghostTween = Tween.UIFillAmount(_ghostFillImage, to, _ghostDropDuration, Ease.InOutCubic);
            Flash(_healColor);
            UpdateLowHpPulse(to);
        }

        /// <summary>Muerte: flash + shake fuerte + fade-out, luego <paramref name="onComplete"/>.</summary>
        public void PlayDeath(Action onComplete)
        {
            if (!Active || _rootGroup == null)
            {
                onComplete?.Invoke();
                return;
            }

            StopLowHp();
            Flash(_flashColor);
            Shake(_shakeMaxStrength * _lethalShakeBoost);
            CameraShake(_deathCamShake);
            Hitstop(_deathHitstop);

            if (_fadeTween.isAlive) _fadeTween.Stop();
            _fadeTween = Tween.Alpha(_rootGroup, 0f, _deathDuration, Ease.InCubic);
            // Secuenciamos el callback con un Delay independiente (robusto al reduced-motion).
            Tween.Delay(_deathDuration, () => onComplete?.Invoke());
        }

        // ------------------------------------------------------------------

        private void Flash(Color flash)
        {
            if (!Active || _fillImage == null) return;
            if (_flashTween.isAlive) _flashTween.Stop();
            _flashTween = Tween.Color(_fillImage, flash, _fillRestColor, _flashDuration, Ease.OutQuad);
        }

        private void Shake(float strength)
        {
            if (!Active || !Motion || _shakeTarget == null || strength <= 0f) return;

            // El rest se toma acá y no en CaptureRest: en este punto el layout ya corrió, así
            // que anchoredPosition es la posición real. Si hay un shake vivo hay que restaurar
            // primero, o capturaríamos una pose ya desplazada por el tween anterior.
            if (_shakeTween.isAlive) { _shakeTween.Stop(); RestoreShakePos(); }
            _shakeRestAnchored = _shakeTarget.anchoredPosition;
            _shakeRestValid = true;

            _shakeTween = Tween.ShakeCustom(_shakeTarget, (Vector3)_shakeRestAnchored,
                new ShakeSettings(new Vector3(strength, strength, 0f), _shakeDuration, _shakeFrequency),
                (rect, value) => rect.anchoredPosition = value);
        }

        private void RestoreShakePos()
        {
            if (_shakeRestValid && _shakeTarget != null) _shakeTarget.anchoredPosition = _shakeRestAnchored;
        }

        // Shake de la cámara del juego vía el servicio compartido (loudest-wins). Corre en
        // unscaled time, así que sigue durante el hitstop.
        private void CameraShake(float amplitude)
        {
            if (!Active || !Motion || amplitude <= 0f) return;
            if (ServiceLocator.TryGetService<ICameraService>(out var cam) && cam != null)
                cam.Shake(amplitude, _camShakeDuration);
        }

        // Hitstop compartido (freeze de Time.timeScale, restore en realtime). No-op fuera de Play.
        private void Hitstop(float seconds)
        {
            if (!Active || !Motion || seconds <= 0f) return;
            DiceAnim.DiceHitstop.Play(seconds);
        }

        private Vector2 FillEdgeLocal(float ratio)
        {
            float w = _barTrack != null ? _barTrack.rect.width : 0f;
            return new Vector2(Mathf.Lerp(-w * 0.5f, w * 0.5f, Mathf.Clamp01(ratio)), 0f);
        }

        private void UpdateLowHpPulse(float ratio)
        {
            bool low = ratio > 0f && ratio <= _lowHpThreshold;
            if (low && Active && Motion && _lifeBorder != null)
            {
                if (!_lowHpTween.isAlive)
                    _lowHpTween = Tween.Color(_lifeBorder, _borderRestColor, _lowHpPulseColor,
                        _lowHpPulsePeriod * 0.5f, Ease.InOutSine, cycles: -1, CycleMode.Yoyo);
            }
            else
            {
                StopLowHp();
            }
        }

        private void StopLowHp()
        {
            if (_lowHpTween.isAlive) _lowHpTween.Stop();
            if (_lifeBorder != null) _lifeBorder.color = _borderRestColor;
        }

        private void StopFillTweens()
        {
            if (_fillTween.isAlive) _fillTween.Stop();
            if (_ghostTween.isAlive) _ghostTween.Stop();
            if (_flashTween.isAlive) _flashTween.Stop();
        }

        private void StopTransformTweens()
        {
            if (_shakeTween.isAlive) _shakeTween.Stop();
            if (_hpPunchTween.isAlive) _hpPunchTween.Stop();
            if (_introTween.isAlive) _introTween.Stop();
            if (_fadeTween.isAlive) _fadeTween.Stop();
        }

        /// <summary>Frena todos los tweens y restaura el reposo — se llama al ocultar la barra.</summary>
        public void StopAndRestore()
        {
            StopFillTweens();
            StopTransformTweens();
            StopLowHp();

            if (_shakeTarget != null) { RestoreShakePos(); _shakeTarget.localScale = _shakeRestScale; }
            if (_hpText != null) _hpText.transform.localScale = _hpRestScale;
            if (_fillImage != null) _fillImage.color = _fillRestColor;
            if (_lifeBorder != null) _lifeBorder.color = _borderRestColor;
            if (_rootGroup != null) _rootGroup.alpha = _rootRestAlpha;
        }
    }
}
