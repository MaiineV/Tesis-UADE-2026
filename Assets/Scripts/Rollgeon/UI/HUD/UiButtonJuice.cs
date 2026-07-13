using MoreMountains.Feedbacks;
using Patterns;
using Rollgeon.Audio;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Juice genérico de botón (Roll, Confirm): squash en el press + click SFX via
    /// <c>IAudioService</c>. Se cuelga junto al <see cref="Button"/> — sin lógica de
    /// gating propia, el onClick ya respeta interactable. Campos opcionales: sin
    /// wiring, no-op.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Ui Button Juice")]
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonJuice : MonoBehaviour
    {
        [SerializeField, Optional, Tooltip("Squash & stretch del press (MMF_ScaleSpring bump).")]
        private MMF_Player _pressPlayer;

        [SerializeField, Optional, Tooltip("Pulso cuando el botón pasa de deshabilitado a habilitado — " +
                 "'ahora podés apretar esto'.")]
        private MMF_Player _availablePulsePlayer;

        [SerializeField, Optional, Tooltip("Click SFX (2D).")]
        private AudioClip _clickClip;

        [SerializeField, Range(0f, 1f)]
        private float _clickVolume = 0.8f;

        private Button _button;
        private bool _lastInteractable;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);
            _lastInteractable = _button.interactable;
        }

        private void Update()
        {
            // Poll barato (2 botones): la transición off→on dispara el pulso de
            // disponibilidad sin acoplar las views que recomputan interactable.
            if (_availablePulsePlayer == null || _button == null) return;
            bool now = _button.interactable;
            if (now && !_lastInteractable)
            {
                if (_availablePulsePlayer.IsPlaying) _availablePulsePlayer.StopFeedbacks();
                _availablePulsePlayer.PlayFeedbacks();
            }
            _lastInteractable = now;
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            if (_pressPlayer != null)
            {
                if (_pressPlayer.IsPlaying) _pressPlayer.StopFeedbacks();
                _pressPlayer.PlayFeedbacks();
            }
            if (_clickClip != null
                && ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
            {
                audio.PlaySfx2D(_clickClip, _clickVolume);
            }
        }
    }
}
