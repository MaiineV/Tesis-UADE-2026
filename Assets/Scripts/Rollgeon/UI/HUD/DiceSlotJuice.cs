using MoreMountains.Feedbacks;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

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

        [Title("Tuning")]
        [SerializeField, Tooltip("Cara que cuenta como crit para la variante dorada del reveal.")]
        private int _critFace = 6;

        private DiceSlotAnimator _animator;

        private void OnEnable()
        {
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
            // ClearAll apaga el slot con feedbacks potencialmente a medias — los
            // springs restauran sus valores iniciales al frenar.
            StopAll();
        }

        /// <summary>Pulso de reaseguro en un reroll: "este dado se queda". Lo dispara <see cref="DiceZoneJuice"/>.</summary>
        public void PlayKeptPulse() => Play(_keptPulsePlayer);

        private void HandleSpinStarted() => Play(_spinStartPlayer);

        private void HandleFaceRevealed(int face)
        {
            var player = face >= _critFace && _critRevealPlayer != null
                ? _critRevealPlayer
                : _revealPlayer;
            Play(player);
        }

        private void HandleLocked() => Play(_lockPlayer);
        private void HandleUnlocked() => Play(_unlockPlayer);
        private void HandleThrow() => Play(_throwPlayer);
        private void HandleDiscarded() => Play(_discardPlayer);

        private static void Play(MMF_Player player)
        {
            if (player == null) return;
            // Replay limpio: un feedback a medias (lock spameado) se corta antes de
            // re-disparar, si no los springs acumulan.
            if (player.IsPlaying) player.StopFeedbacks();
            player.PlayFeedbacks();
        }

        private void StopAll()
        {
            Stop(_spinStartPlayer);
            Stop(_revealPlayer);
            Stop(_critRevealPlayer);
            Stop(_lockPlayer);
            Stop(_unlockPlayer);
            Stop(_throwPlayer);
            Stop(_discardPlayer);
            Stop(_keptPulsePlayer);
        }

        private static void Stop(MMF_Player player)
        {
            if (player != null && player.IsPlaying) player.StopFeedbacks();
        }
    }
}
