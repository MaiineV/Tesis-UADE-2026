using MoreMountains.Feedbacks;
using PrimeTween;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Capa de juice visual (Feel) de un slot de dado legacy: escucha los hooks del
    /// <see cref="DiceSlotAnimator"/> y dispara los <see cref="MMF_Player"/> autorados
    /// en el prefab. Contrato anti-pelea con la capa de motion (que es dueña de
    /// posición y alpha del root siempre, de la rotación durante el spin y de la
    /// escala durante el outro): los feedbacks de scale/rotación del root solo se
    /// disparan en fases sin overlap (spin-start, reveal, lock/unlock, kept-pulse);
    /// los flashes van al hijo FlashOverlay y el desaturado al DiceLabel. Nadie toca
    /// el color del background — es de <see cref="DiceSlotView"/>. Todos los campos
    /// son opcionales: sin wiring, no-op. El audio NO vive acá — lo centraliza
    /// <see cref="DiceZoneJuice"/> vía <c>IAudioService</c> (TECHNICAL.md §17), que
    /// además necesita estado de zona para los pitch ramps.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Slot Juice")]
    public sealed class DiceSlotJuice : MonoBehaviour
    {
        [Title("Players (hijo Juice del prefab)")]
        [SerializeField, Optional, Tooltip("Squash de anticipación al arrancar el spin.")]
        private MMF_Player _spinStartPlayer;

        [SerializeField, Optional, Tooltip("Punch + flash al revelar la cara.")]
        private MMF_Player _revealPlayer;

        [SerializeField, Optional, Tooltip("Variante dorada para el crit. Vacío = usa el reveal normal.")]
        private MMF_Player _critRevealPlayer;

        [SerializeField, Optional, Tooltip("Wobble + flash cyan al holdear.")]
        private MMF_Player _lockPlayer;

        [SerializeField, Optional, Tooltip("Dip al des-holdear.")]
        private MMF_Player _unlockPlayer;

        [SerializeField, Optional, Tooltip("Stretch direccional al volar al centro.")]
        private MMF_Player _throwPlayer;

        [SerializeField, Optional, Tooltip("Desaturado del label al descartarse.")]
        private MMF_Player _discardPlayer;

        [SerializeField, Optional, Tooltip("Pulso de 'este se queda' cuando otros dados rerollean.")]
        private MMF_Player _keptPulsePlayer;

        [Title("Held — Glow y partículas")]
        [SerializeField, Optional, Tooltip("Overlay que pulsa (alpha yoyo) mientras el dado está holdeado.")]
        private Graphic _glowOverlay;

        [SerializeField, Optional, Tooltip("Sparkles idle mientras el dado está holdeado.")]
        private ParticleSystem _holdSparkles;

        [SerializeField, Optional, Tooltip("Puff al revelar la cara (aterrizaje del spin).")]
        private ParticleSystem _revealPuff;

        [Title("Tuning")]
        [SerializeField, Tooltip("Cara que cuenta como crit para la variante dorada del reveal.")]
        private int _critFace = 6;

        [SerializeField, Tooltip("Desde esta cara (sin llegar a crit) el reveal pega más fuerte.")]
        private int _highFace = 4;

        [SerializeField, Tooltip("Multiplicador de intensidad MMF para caras altas no-crit.")]
        private float _highFaceIntensity = 1.3f;

        [SerializeField, Range(0f, 1f), Tooltip("Alpha máximo del glow pulsante del held.")]
        private float _glowMaxAlpha = 0.15f;

        [SerializeField, Tooltip("Período (segundos) de medio ciclo del pulso de glow.")]
        private float _glowPulseSeconds = 0.7f;

        private DiceSlotAnimator _animator;
        private Tween _glowTween;

        private void OnEnable()
        {
            MmfJuice.CaptureRestPose(_spinStartPlayer);
            MmfJuice.CaptureRestPose(_revealPlayer);
            MmfJuice.CaptureRestPose(_critRevealPlayer);
            MmfJuice.CaptureRestPose(_lockPlayer);
            MmfJuice.CaptureRestPose(_unlockPlayer);
            MmfJuice.CaptureRestPose(_throwPlayer);
            MmfJuice.CaptureRestPose(_discardPlayer);
            MmfJuice.CaptureRestPose(_keptPulsePlayer);
            TryHook();
        }

        private void Update()
        {
            // El DiceSlotAnimator lo agrega DiceZoneAnimator.Bind por código — puede
            // no existir todavía cuando este OnEnable corre. Reintento hasta engancharlo.
            if (_animator == null) TryHook();
        }

        private void TryHook()
        {
            if (_animator != null) return;
            _animator = GetComponent<DiceSlotAnimator>();
            if (_animator == null) return;
            _animator.SpinStarted += HandleSpinStarted;
            _animator.FaceRevealed += HandleFaceRevealed;
            _animator.DieLocked += HandleLocked;
            _animator.DieUnlocked += HandleUnlocked;
            _animator.ThrowStarted += HandleThrow;
            _animator.DieDiscarded += HandleDiscarded;
        }

        private void OnDisable()
        {
            if (_animator != null)
            {
                _animator.SpinStarted -= HandleSpinStarted;
                _animator.FaceRevealed -= HandleFaceRevealed;
                _animator.DieLocked -= HandleLocked;
                _animator.DieUnlocked -= HandleUnlocked;
                _animator.ThrowStarted -= HandleThrow;
                _animator.DieDiscarded -= HandleDiscarded;
                _animator = null;
            }
            // ClearAll apaga el slot con feedbacks potencialmente a medias — hay que
            // devolver los springs a su reposo explícitamente (ver MmfJuice).
            StopAll();
            StopGlow();
        }

        /// <summary>Pulso de reaseguro en un reroll: "este dado se queda". Lo dispara <see cref="DiceZoneJuice"/>.</summary>
        public void PlayKeptPulse() => Play(_keptPulsePlayer);

        private void HandleSpinStarted() => Play(_spinStartPlayer);

        private void HandleFaceRevealed(int face)
        {
            if (_revealPuff != null) _revealPuff.Play();

            if (face >= _critFace && _critRevealPlayer != null)
            {
                Play(_critRevealPlayer);
                return;
            }
            // Escalera de impacto: las caras altas no-crit pegan más fuerte (la
            // intensidad MMF multiplica los bumps de los springs y el flash).
            float intensity = face >= _highFace ? _highFaceIntensity : 1f;
            Play(_revealPlayer, intensity);
        }

        private void HandleLocked()
        {
            Play(_lockPlayer);
            StartGlow();
            if (_holdSparkles != null) _holdSparkles.Play();
        }

        private void HandleUnlocked()
        {
            Play(_unlockPlayer);
            StopGlow();
        }

        private void HandleThrow()
        {
            Play(_throwPlayer);
            StopGlow();
        }

        private void HandleDiscarded()
        {
            Play(_discardPlayer);
            StopGlow();
        }

        // Glow pulsante mientras el dado está holdeado — feedback constante de
        // estado, complementa el tint azul del background (que es de DiceSlotView).
        private void StartGlow()
        {
            if (_glowOverlay == null || _glowPulseSeconds <= 0f) return;
            if (_glowTween.isAlive) _glowTween.Stop();
            var overlay = _glowOverlay;
            _glowTween = Tween.Custom(0f, _glowMaxAlpha, _glowPulseSeconds,
                cycles: -1, cycleMode: CycleMode.Yoyo, ease: Ease.InOutSine,
                onValueChange: a =>
                {
                    if (overlay == null) return;
                    var c = overlay.color;
                    c.a = a;
                    overlay.color = c;
                });
        }

        private void StopGlow()
        {
            if (_glowTween.isAlive) _glowTween.Stop();
            if (_glowOverlay != null)
            {
                var c = _glowOverlay.color;
                c.a = 0f;
                _glowOverlay.color = c;
            }
            if (_holdSparkles != null)
                _holdSparkles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void Play(MMF_Player player, float intensity = 1f)
            => MmfJuice.Replay(player, intensity);

        private void StopAll()
        {
            MmfJuice.Rest(_spinStartPlayer);
            MmfJuice.Rest(_revealPlayer);
            MmfJuice.Rest(_critRevealPlayer);
            MmfJuice.Rest(_lockPlayer);
            MmfJuice.Rest(_unlockPlayer);
            MmfJuice.Rest(_throwPlayer);
            MmfJuice.Rest(_discardPlayer);
            MmfJuice.Rest(_keptPulsePlayer);
        }
    }
}
