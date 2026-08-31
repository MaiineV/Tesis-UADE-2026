using MoreMountains.Feedbacks;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Rollgeon.GameCamera;
using Rollgeon.Items;
using Rollgeon.Timing;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Breakdown;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Todo el juice del reveal gacha: SFX vía <c>IAudioService</c>, pointer flick,
    /// flash, burst de partículas, shakes, hitstop, springs MMF, duck de música y
    /// pulses idle — escalado por rareza con <see cref="ChestRevealFeelMath"/>.
    /// <b>Fire-and-forget</b> (patrón <c>BreakdownJuice</c>): jamás participa de la
    /// cadena <c>onDone</c> del player; la view lo llama null-safe y sin settings o
    /// refs cada método es no-op. Cleanup garantizado: <see cref="OnSequenceEnd"/>
    /// corre en los 3 caminos de cierre y <c>OnDisable</c> lo duplica.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Chest Reveal/Chest Reveal Juice")]
    public sealed class ChestRevealJuice : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Optional] private ChestRevealUiSettingsSO _settings;
        [SerializeField, Optional] private ScreenFlashView _flash;
        [SerializeField, Optional] private DiceThrowImpactBurst _burst;
        [SerializeField, Optional] private RectTransform _shakeTarget;
        [SerializeField, Optional] private RectTransform _pointer;
        [SerializeField, Optional] private Transform _titleTransform;
        [SerializeField, Optional, Tooltip("Target del zoom de anticipación del climax (viewport del reel).")]
        private RectTransform _zoomTarget;
        [SerializeField, Optional, Tooltip("Target del pulse idle de la card durante WaitDismiss.")]
        private RectTransform _cardPulseTarget;
        [SerializeField, Optional, Tooltip("Hint que respira durante WaitDismiss.")]
        private TMP_Text _hintLabel;

        [Title("Feel (MMF) — canales posición/rotación, PrimeTween hace scale/alpha")]
        [SerializeField, Optional] private MMF_Player _openPlayer;
        [SerializeField, Optional] private MMF_Player _revealPlayer;

        [Title("Clips (placeholder — reemplazar cuando haya SFX propios del cofre)")]
        [SerializeField, Optional] private AudioClip _tickClip;
        [SerializeField, Optional] private AudioClip _whooshClip;
        [SerializeField, Optional] private AudioClip _landThunkClip;
        [SerializeField, Optional] private AudioClip _chimeClip;
        [SerializeField, Optional] private AudioClip _countTickClip;
        [SerializeField, Optional] private AudioClip _cardSlideClip;
        [SerializeField, Optional] private AudioClip _clickClip;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 0.8f;

        private float _nextSfxTickAt;
        private bool _ducked;

        private Tween _shakeTween;
        private Vector2 _shakeRestAnchored;
        private bool _shakeRestValid;

        private Tween _pointerFlick;
        private Tween _zoomTween;
        private bool _zoomed;

        private Tween _framePulse;
        private Graphic _pulsedFrame;
        private Color _pulsedFrameRest;
        private Tween _cardPulse;
        private Tween _hintPulse;
        private float _hintRestAlpha = 1f;

        // ------------------------------------------------------------------
        // Gates — mismo idiom que BossBarJuice/BreakdownJuice. El audio NO se
        // gatea por ReducedMotion: reduced motion corta movimiento, no sonido.
        // ------------------------------------------------------------------
        private static bool Active => Application.isPlaying;
        private static bool Motion => !DiceUiMotionPrefs.ReducedMotion;
        private bool Sfx => Active && (_settings == null || _settings.EnableSfx);
        private bool Particles => Active && Motion && (_settings == null || _settings.EnableParticles);
        private bool ShakeOk => Active && Motion && (_settings == null || _settings.EnableShakeAndHitstop);

        private void OnEnable()
        {
            // Reposo de los springs con el target quieto (ver doc de MmfJuice).
            MmfJuice.CaptureRestPose(_openPlayer);
            MmfJuice.CaptureRestPose(_revealPlayer);
            if (_hintLabel != null) _hintRestAlpha = _hintLabel.color.a;
        }

        private void OnDisable() => OnSequenceEnd();

        // ==================================================================
        // Beat 0 — Open
        // ==================================================================

        public void OnOpen()
        {
            PlaySfx(_whooshClip, _sfxVolume * 0.5f, 1.05f);
        }

        /// <summary>El panel terminó su pop de entrada: thump del título.</summary>
        public void OnPanelLanded()
        {
            PlaySfx(_cardSlideClip, _sfxVolume * 0.4f);
            if (!Motion || _settings == null) return;

            if (_titleTransform != null && _settings.TitlePunchScale > 0f)
                Tween.PunchScale(_titleTransform, Vector3.one * _settings.TitlePunchScale,
                    _settings.TitlePunchSeconds, frequency: 2);
            if (_openPlayer != null) MmfJuice.Replay(_openPlayer);
        }

        // ==================================================================
        // Beat 1 — Spin
        // ==================================================================

        public void OnSpinStart()
        {
            _nextSfxTickAt = 0f;
        }

        /// <summary>
        /// Una celda pasó bajo el puntero. El pitch sube con el progreso (la
        /// desaceleración espacia los ticks sola = anticipación). Rate-limited
        /// dividido por la velocidad de juego para no saturar el mixer a x4.
        /// </summary>
        public void OnReelTick(int cellIndex, float spinProgress01)
        {
            if (_settings == null) return;

            if (Sfx && Time.unscaledTime >= _nextSfxTickAt)
            {
                _nextSfxTickAt = ChestRevealFeelMath.NextTickTime(
                    Time.unscaledTime, _settings.TickMinInterval, GameSpeedPrefs.Multiplier);
                PlaySfx(_tickClip, _settings.TickVolume * _sfxVolume,
                    ChestRevealFeelMath.TickPitch(spinProgress01,
                        _settings.TickBasePitch, _settings.TickMaxPitch));
            }

            // Flick físico del puntero. Shake* restaura solo al completar — guard
            // isAlive en vez de StopAll (patrón ActionButton).
            if (Motion && _pointer != null && _settings.PointerFlickDegrees > 0f && !_pointerFlick.isAlive)
            {
                _pointerFlick = Tween.ShakeLocalRotation(_pointer,
                    new Vector3(0f, 0f, _settings.PointerFlickDegrees),
                    _settings.PointerFlickSeconds, frequency: 10f, useUnscaledTime: true);
            }
        }

        /// <summary>Anticipación del landing (una vez, cerca del final del spin).</summary>
        public void OnSpinClimax(ItemRarity tier)
        {
            if (_settings == null) return;

            if (Motion && _zoomTarget != null && _settings.ClimaxZoomScale > 1f)
            {
                if (_zoomTween.isAlive) _zoomTween.Stop();
                _zoomed = true;
                _zoomTween = Tween.Scale(_zoomTarget, Vector3.one * _settings.ClimaxZoomScale,
                    _settings.ClimaxZoomSeconds, Ease.OutQuad, useUnscaledTime: true);
            }

            if (ChestRevealFeelMath.DuckAllowed(tier))
                DuckMusic(_settings.ClimaxDuckFactor);
        }

        // ==================================================================
        // Beat 2 — Landing / Reveal (escalado por rareza)
        // ==================================================================

        public void OnRewardRevealed(RectTransform winnerRect, ItemRarity rarity)
        {
            if (_settings == null) return;
            float k = ChestRevealFeelMath.Intensity01(rarity);

            // El zoom de anticipación vuelve a reposo con el impacto.
            if (_zoomed && _zoomTarget != null)
            {
                if (_zoomTween.isAlive) _zoomTween.Stop();
                _zoomTween = Tween.Scale(_zoomTarget, Vector3.one, 0.15f, Ease.OutQuad, useUnscaledTime: true);
                _zoomed = false;
            }

            if (Motion && _flash != null)
            {
                float peak = ChestRevealFeelMath.Knob(0f, _settings.FlashPeakAlphaMax, k);
                // Common queda por debajo del umbral perceptible — mejor nada que un parpadeo sucio.
                if (peak >= 0.05f)
                    _flash.Flash(RarityPalette.BodyColor(rarity), peak, _settings.FlashSeconds);
            }

            if (Particles && _burst != null && winnerRect != null)
                _burst.Burst(ToBurstSpace(winnerRect), Vector2.up,
                    ChestRevealFeelMath.Knob(_settings.BurstIntensityMin, _settings.BurstIntensityMax, k));

            if (ShakeOk)
            {
                ShakePanel(k * _settings.PanelShakeAmplitudeMax);

                if (ChestRevealFeelMath.DuckAllowed(rarity))
                    CameraShake(ChestRevealFeelMath.Knob(0f, _settings.CamShakeAmplitudeMax, k),
                        _settings.CamShakeSeconds);

                if (ChestRevealFeelMath.HitstopAllowed(rarity) && _settings.HitstopSeconds > 0f)
                    DiceHitstop.Play(_settings.HitstopSeconds);
            }

            if (Motion && winnerRect != null)
            {
                float punch = ChestRevealFeelMath.Knob(
                    _settings.WinnerPunchScaleMin, _settings.WinnerPunchScaleMax, k);
                Tween.StopAll(onTarget: winnerRect);
                winnerRect.localScale = Vector3.one;
                Tween.Scale(winnerRect, Vector3.one * punch, _settings.WinnerPunchSeconds,
                        Ease.OutBack, useUnscaledTime: true)
                    .OnComplete(winnerRect, r =>
                        Tween.Scale(r, Vector3.one, 0.12f, Ease.InOutQuad, useUnscaledTime: true));
            }

            if (Motion && _revealPlayer != null) MmfJuice.Replay(_revealPlayer, Mathf.Max(0.2f, k));

            PlaySfx(_landThunkClip, _sfxVolume * 0.8f);
            PlaySfx(_chimeClip, _sfxVolume,
                ChestRevealFeelMath.Knob(_settings.ChimePitchMin, _settings.ChimePitchMax, k),
                isImportant: rarity >= ItemRarity.Rare);

            if (ChestRevealFeelMath.DuckAllowed(rarity))
                DuckMusic(_settings.RevealDuckFactor);
        }

        public void OnCardShown()
        {
            PlaySfx(_cardSlideClip, _sfxVolume * 0.6f, 1.1f);
        }

        /// <summary>Tick del count-up del oro — comparte rate-limit con el tick del reel.</summary>
        public void OnCountUpTick(float progress01)
        {
            if (_settings == null || !Sfx) return;
            if (Time.unscaledTime < _nextSfxTickAt) return;
            _nextSfxTickAt = ChestRevealFeelMath.NextTickTime(
                Time.unscaledTime, _settings.TickMinInterval, GameSpeedPrefs.Multiplier);
            PlaySfx(_countTickClip, _sfxVolume * 0.5f, Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(progress01)));
        }

        // ==================================================================
        // Beat 3 — WaitDismiss (idles)
        // ==================================================================

        /// <summary>
        /// Arranca los pulses idle. Se llama desde WaitDismiss y NO desde el reveal:
        /// el skip Jump saltea PlayReveal, pero siempre aterriza acá.
        /// </summary>
        public void OnWaitDismiss(ChestReelCellView winner, ItemRarity rarity)
        {
            if (!Motion || _settings == null) return;

            var bg = winner != null ? winner.BackgroundGraphic : null;
            if (bg != null && !_framePulse.isAlive && _settings.IdlePulseColorLerp > 0f)
            {
                // El pulse OSCURECE el tinte (rest = blanco): el color de rareza vive
                // en el sprite del fondo, aclarar hacia blanco sería invisible.
                _pulsedFrame = bg;
                _pulsedFrameRest = bg.color;
                _framePulse = Tween.Color(bg,
                    Color.Lerp(_pulsedFrameRest, Color.black, _settings.IdlePulseColorLerp),
                    _settings.IdlePulsePeriod * 0.5f, Ease.InOutSine,
                    cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);
            }

            if (_cardPulseTarget != null && !_cardPulse.isAlive && _settings.IdlePulseScale > 1f)
            {
                _cardPulseTarget.localScale = Vector3.one;
                _cardPulse = Tween.Scale(_cardPulseTarget, Vector3.one * _settings.IdlePulseScale,
                    _settings.IdlePulsePeriod, Ease.InOutSine,
                    cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);
            }

            if (_hintLabel != null && !_hintPulse.isAlive)
            {
                _hintPulse = Tween.Alpha(_hintLabel, _hintRestAlpha * 0.55f,
                    _settings.IdlePulsePeriod, Ease.InOutSine,
                    cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);
            }
        }

        public void OnDismissRequested()
        {
            PlaySfx(_clickClip, _sfxVolume * 0.7f);
        }

        // ==================================================================
        // Beat 4 — Close / cleanup
        // ==================================================================

        public void OnClose()
        {
            PlaySfx(_whooshClip, _sfxVolume * 0.3f, 0.85f);
        }

        /// <summary>
        /// Skip Jump / watchdog: frena todo lo que esté en vuelo y restaura poses.
        /// El duck NO se toca acá — la música vuelve recién en <see cref="OnSequenceEnd"/>.
        /// BUG-071: los springs MMF también se restauran — un stop sin restore los
        /// re-basea (<c>_targetValue = _currentValue</c>) y el próximo cofre arranca
        /// desde la pose desplazada, acumulativo.
        /// </summary>
        public void OnForceFinalState()
        {
            StopMotionResiduals();
            MmfJuice.Rest(_openPlayer);
            MmfJuice.Rest(_revealPlayer);
        }

        /// <summary>
        /// Cierre — SIEMPRE (normal/skip/watchdog/teardown) y ANTES del próximo
        /// cofre de la cola: un pulse de color vivo pisaría el re-Bind del reel.
        /// Idempotente a propósito (OnDisable lo repite).
        /// </summary>
        public void OnSequenceEnd()
        {
            StopMotionResiduals();
            if (_ducked)
            {
                DuckMusicRaw(1f);
                _ducked = false;
            }
            MmfJuice.Rest(_openPlayer);
            MmfJuice.Rest(_revealPlayer);
        }

        // ==================================================================
        // Internos
        // ==================================================================

        private void StopMotionResiduals()
        {
            // BUG-071: restaurar SIEMPRE que haya rest válido, no solo con el tween
            // vivo — la view mata el shake desde afuera (Tween.StopAll sobre el panel)
            // y sin este restore la posición derivada se re-capturaba como reposo en
            // el próximo ShakePanel, acumulando corrimiento cofre a cofre.
            if (_shakeTween.isAlive) _shakeTween.Stop();
            RestoreShakePos();
            if (_pointerFlick.isAlive)
            {
                _pointerFlick.Stop();
                if (_pointer != null) _pointer.localRotation = Quaternion.identity;
            }
            if (_zoomTween.isAlive) _zoomTween.Stop();
            if (_zoomTarget != null) _zoomTarget.localScale = Vector3.one;
            _zoomed = false;

            if (_framePulse.isAlive) _framePulse.Stop();
            if (_pulsedFrame != null)
            {
                _pulsedFrame.color = _pulsedFrameRest;
                _pulsedFrame = null;
            }
            if (_cardPulse.isAlive) _cardPulse.Stop();
            if (_cardPulseTarget != null) _cardPulseTarget.localScale = Vector3.one;
            if (_hintPulse.isAlive) _hintPulse.Stop();
            if (_hintLabel != null)
            {
                var c = _hintLabel.color;
                c.a = _hintRestAlpha;
                _hintLabel.color = c;
            }
        }

        // Shake del panel con rest capturado FRESCO por disparo — nunca en OnEnable
        // (con anclas no centradas el layout puede no haber corrido; ver BossBarJuice).
        private void ShakePanel(float amplitude)
        {
            if (_shakeTarget == null || amplitude <= 0f || _settings == null) return;
            if (_shakeTween.isAlive)
            {
                _shakeTween.Stop();
                RestoreShakePos();
            }
            _shakeRestAnchored = _shakeTarget.anchoredPosition;
            _shakeRestValid = true;
            _shakeTween = Tween.ShakeCustom(_shakeTarget, (Vector3)_shakeRestAnchored,
                new ShakeSettings(new Vector3(amplitude, amplitude, 0f),
                    _settings.PanelShakeSeconds, _settings.PanelShakeFrequency),
                (rect, value) => rect.anchoredPosition = value);
        }

        private void RestoreShakePos()
        {
            if (!_shakeRestValid || _shakeTarget == null) return;
            _shakeTarget.anchoredPosition = _shakeRestAnchored;
            _shakeRestValid = false;
        }

        // Proyección al espacio local del contenedor del burst (patrón BreakdownJuice).
        private Vector2 ToBurstSpace(RectTransform anchor)
        {
            if (_burst == null || anchor == null) return Vector2.zero;
            var container = (RectTransform)_burst.transform;
            return (Vector2)container.InverseTransformPoint(anchor.position);
        }

        private void PlaySfx(AudioClip clip, float volume, float pitch = 1f, bool isImportant = false)
        {
            if (!Sfx || clip == null) return;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(clip, volume, pitch, isImportant);
        }

        private void DuckMusic(float factor)
        {
            if (!Sfx || factor >= 1f) return;
            DuckMusicRaw(factor);
            _ducked = true;
        }

        private void DuckMusicRaw(float factor)
        {
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.DuckMusic(factor, 0.2f);
        }

        private void CameraShake(float amplitude, float seconds)
        {
            if (amplitude <= 0f) return;
            if (ServiceLocator.TryGetService<ICameraService>(out var cam) && cam != null)
                cam.Shake(amplitude, seconds);
        }
    }
}
