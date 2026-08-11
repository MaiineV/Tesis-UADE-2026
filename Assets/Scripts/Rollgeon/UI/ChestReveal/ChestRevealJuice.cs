using PrimeTween;
using Rollgeon.Items;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Juice fire-and-forget del reveal (patrón <c>BossBarJuice</c>): refs opcionales
    /// ⇒ no-op, gates <c>Application.isPlaying</c> + <c>ReducedMotion</c>. La lógica
    /// de secuencia vive en <see cref="ChestRevealView"/>; acá solo condimento.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Chest Reveal/Chest Reveal Juice")]
    public sealed class ChestRevealJuice : MonoBehaviour
    {
        [Title("Audio (opcional)")]
        [SerializeField] private AudioSource _tickAudio;

        [Tooltip("Incremento de pitch por celda — el tick sube de tono a medida que avanza.")]
        [SerializeField] private float _tickPitchStep = 0.01f;

        [SerializeField] private float _tickBasePitch = 0.9f;
        [SerializeField] private float _tickMaxPitch = 1.6f;

        [Title("Reveal")]
        [SerializeField] private float _winnerPunchScale = 1.18f;
        [SerializeField] private float _winnerPunchSeconds = 0.18f;

        [Tooltip("Shake extra del panel cuando el reward es Legendary.")]
        [SerializeField] private float _legendaryShakeAmplitude = 6f;
        [SerializeField] private RectTransform _shakeTarget;

        private bool CanJuice => Application.isPlaying && !DiceUiMotionPrefs.ReducedMotion;

        public void OnReelTick(int cellIndex)
        {
            if (!Application.isPlaying || _tickAudio == null || _tickAudio.clip == null) return;
            _tickAudio.pitch = Mathf.Min(_tickMaxPitch, _tickBasePitch + cellIndex * _tickPitchStep);
            _tickAudio.PlayOneShot(_tickAudio.clip);
        }

        public void OnRewardRevealed(RectTransform winnerRect, ItemRarity rarity)
        {
            if (!CanJuice) return;

            if (winnerRect != null)
            {
                Tween.StopAll(onTarget: winnerRect);
                winnerRect.localScale = Vector3.one;
                Tween.Scale(winnerRect, Vector3.one * _winnerPunchScale, _winnerPunchSeconds,
                        Ease.OutBack, useUnscaledTime: true)
                    .OnComplete(winnerRect, r =>
                        Tween.Scale(r, Vector3.one, 0.12f, Ease.InOutQuad, useUnscaledTime: true));
            }

            if (rarity == ItemRarity.Legendary && _shakeTarget != null && _legendaryShakeAmplitude > 0f)
            {
                Tween.ShakeCustom(_shakeTarget, _shakeTarget.anchoredPosition,
                    new ShakeSettings(Vector3.one * _legendaryShakeAmplitude, 0.35f, frequency: 12f),
                    (target, value) => target.anchoredPosition = (Vector2)value);
            }
        }
    }
}
