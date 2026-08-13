using MoreMountains.Feedbacks;
using Patterns;
using PrimeTween;
using Rollgeon.Audio;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Juice de la transición de skin del tablero: cuando <see cref="DiceBoardSkinView"/>
    /// cambia de <see cref="DiceBoardType"/> (ataque ↔ defensa ↔ default), flashea el tint
    /// del board, hace fade-in + punch del logo y dispara <see cref="MMF_Player"/>s
    /// autorables. Vive en el mismo GameObject que el View y escucha su
    /// <c>BoardTypeChanged</c> — que solo dispara en cambios reales, así que acá no hay
    /// gates de estado. Todos los campos son opcionales: sin wiring, no-op.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Board Skin Juice")]
    public sealed class DiceBoardSkinJuice : MonoBehaviour
    {
        [Title("Targets (mismos del View)")]
        [SerializeField, Optional, Tooltip("Image del tablero (el mismo _boardImage del View). Sin ref, no hay flash de tint.")]
        private Image _boardImage;

        [SerializeField, Optional, Tooltip("Image del DiceBoardLogo (el mismo _logoImage del View). Sin ref, no hay fade/punch de logo.")]
        private Image _logoImage;

        [Title("Players (autorables en Feel — sin MMF_Sound, el audio va por _transitionClip)")]
        [SerializeField, Optional, Tooltip("Player genérico al cambiar el tipo de tablero (cualquier destino).")]
        private MMF_Player _transitionPlayer;

        [SerializeField, Optional, Tooltip("Extra al pasar a Attack (además del genérico).")]
        private MMF_Player _toAttackPlayer;

        [SerializeField, Optional, Tooltip("Extra al pasar a Defense (además del genérico).")]
        private MMF_Player _toDefensePlayer;

        [Title("Tuning")]
        [SerializeField, Tooltip("Duración del flash de tint del board. 0 = sin flash.")]
        private float _tintFlashDuration = 0.25f;

        [SerializeField, Range(0f, 1f), Tooltip("Cuánto blanco mezcla el flash antes de asentarse en el tint del tipo.")]
        private float _tintFlashAmount = 0.6f;

        [SerializeField, Tooltip("Duración del fade-in del logo nuevo. 0 = sin fade.")]
        private float _logoFadeDuration = 0.2f;

        [SerializeField, Tooltip("Fuerza del punch de escala del logo (0.15 = ±15%). 0 = sin punch.")]
        private float _logoPunchScale = 0.15f;

        [SerializeField, Tooltip("Duración del punch de escala del logo.")]
        private float _logoPunchDuration = 0.3f;

        [Title("Idle del logo (por tipo — llama la atención en reposo)")]
        [SerializeField, Tooltip("Idle del logo en Attack. Default: Pulse (latido agresivo de escala).")]
        private LogoIdle _attackIdle = new() { Style = LogoIdleStyle.Pulse, Amplitude = 0.08f, Period = 0.9f };

        [SerializeField, Tooltip("Idle del logo en Defense. Default: Bob (flote calmo vertical).")]
        private LogoIdle _defenseIdle = new() { Style = LogoIdleStyle.Bob, Amplitude = 3f, Period = 2.2f };

        [SerializeField, Tooltip("Idle del logo en Default (normalmente sin logo, así que None).")]
        private LogoIdle _defaultIdle = new() { Style = LogoIdleStyle.None };

        [Title("SFX (via IAudioService, 2D)")]
        [SerializeField, Optional, Tooltip("Swish/stinger al cambiar el tipo de tablero.")]
        private AudioClip _transitionClip;

        private DiceBoardSkinView _view;
        private Tween _boardFlashTween;
        private Tween _logoFadeTween;
        private Tween _logoPunchTween;
        private Tween _logoIdleTween;
        private Vector2 _logoRestPos;
        private bool _logoRestCaptured;

        private void OnEnable()
        {
            MmfJuice.CaptureRestPose(_transitionPlayer);
            MmfJuice.CaptureRestPose(_toAttackPlayer);
            MmfJuice.CaptureRestPose(_toDefensePlayer);

            if (_logoImage != null && !_logoRestCaptured)
            {
                _logoRestPos = _logoImage.rectTransform.anchoredPosition;
                _logoRestCaptured = true;
            }

            // El View se agrega a mano en el prefab (no por código) — sin retry loop.
            _view = GetComponent<DiceBoardSkinView>();
            if (_view != null)
            {
                _view.BoardTypeChanged += HandleBoardTypeChanged;
                // La zona pudo re-habilitarse con un tipo ya aplicado (el View conserva
                // su current): el idle retoma sin esperar una transición nueva.
                StartIdle(_view.CurrentType);
            }
        }

        private void OnDisable()
        {
            if (_view != null)
            {
                _view.BoardTypeChanged -= HandleBoardTypeChanged;
                _view = null;
            }
            StopAllTweens();
            // Zona escondida a mitad del feedback: sin esto los springs re-basan su
            // reposo al valor desplazado del momento (ver MmfJuice).
            MmfJuice.Rest(_transitionPlayer);
            MmfJuice.Rest(_toAttackPlayer);
            MmfJuice.Rest(_toDefensePlayer);
        }

        private void HandleBoardTypeChanged(DiceBoardType type)
        {
            // Los tests EditMode llaman ApplyBoardType y el evento dispara igual —
            // PrimeTween/Feel solo corren en Play Mode (mismo guard que
            // ValueTextFeedbackController.SetBoardType).
            if (!Application.isPlaying) return;

            // El View ya aplicó el estado final antes de invocar el evento: los colores
            // actuales de los Images SON el destino. Los capturamos ANTES de StopAllTweens,
            // que normaliza el alpha del logo a 1 y pisaría un tint autorado translúcido.
            var boardTarget = _boardImage != null ? _boardImage.color : default;
            var logoTarget = _logoImage != null ? _logoImage.color : default;

            StopAllTweens();

            if (_boardImage != null && _tintFlashDuration > 0f)
            {
                _boardFlashTween = Tween.Color(_boardImage,
                    Color.Lerp(boardTarget, Color.white, _tintFlashAmount), boardTarget,
                    _tintFlashDuration, Ease.OutQuad);
            }

            if (_logoImage != null && _logoImage.enabled)
            {
                if (_logoFadeDuration > 0f)
                {
                    var start = logoTarget;
                    start.a = 0f;
                    _logoFadeTween = Tween.Color(_logoImage, start, logoTarget, _logoFadeDuration);
                }
                if (_logoPunchScale > 0f && _logoPunchDuration > 0f)
                {
                    _logoPunchTween = Tween.PunchScale(_logoImage.rectTransform,
                        Vector3.one * _logoPunchScale, _logoPunchDuration, frequency: 4);
                }
            }

            MmfJuice.Replay(_transitionPlayer);
            switch (type)
            {
                case DiceBoardType.Attack: MmfJuice.Replay(_toAttackPlayer); break;
                case DiceBoardType.Defense: MmfJuice.Replay(_toDefensePlayer); break;
            }

            PlaySfx(_transitionClip, volume: 0.8f);

            StartIdle(type);
        }

        /// <summary>
        /// Arranca el idle del logo para <paramref name="type"/>. Pulse arranca con
        /// delay del punch de transición (ambos escriben scale); Bob usa posición y
        /// Sway rotación, que solo comparten canal con los springs de Feel durante
        /// el estallido inicial — el spring gana ese ratito y después queda el idle.
        /// </summary>
        private void StartIdle(DiceBoardType type)
        {
            if (!Application.isPlaying) return;
            if (_logoIdleTween.isAlive) _logoIdleTween.Stop();
            if (_logoImage == null || !_logoImage.enabled) return;

            var idle = type switch
            {
                DiceBoardType.Attack => _attackIdle,
                DiceBoardType.Defense => _defenseIdle,
                _ => _defaultIdle,
            };
            if (idle.Style == LogoIdleStyle.None || idle.Period <= 0f) return;

            var rt = _logoImage.rectTransform;
            float half = idle.Period * 0.5f;
            switch (idle.Style)
            {
                case LogoIdleStyle.Pulse:
                    _logoIdleTween = Tween.Scale(rt, Vector3.one, Vector3.one * (1f + idle.Amplitude),
                        half, Ease.InOutSine, cycles: -1, CycleMode.Yoyo,
                        startDelay: _logoPunchDuration);
                    break;
                case LogoIdleStyle.Bob:
                    _logoIdleTween = Tween.UIAnchoredPositionY(rt,
                        _logoRestPos.y - idle.Amplitude, _logoRestPos.y + idle.Amplitude,
                        half, Ease.InOutSine, cycles: -1, CycleMode.Yoyo);
                    break;
                case LogoIdleStyle.Sway:
                    _logoIdleTween = Tween.LocalRotation(rt,
                        Quaternion.Euler(0f, 0f, -idle.Amplitude), Quaternion.Euler(0f, 0f, idle.Amplitude),
                        half, Ease.InOutSine, cycles: -1, CycleMode.Yoyo);
                    break;
            }
        }

        /// <summary>
        /// Frena los tweens vivos y restaura el reposo de sus targets — Stop de PrimeTween
        /// congela el valor del momento, y un logo semitransparente/escalado a mitad de
        /// fade quedaría así permanente. El tint del board no necesita restore: el View
        /// re-aplica el color final en cada ApplyBoardType.
        /// </summary>
        private void StopAllTweens()
        {
            if (_boardFlashTween.isAlive) _boardFlashTween.Stop();
            if (_logoFadeTween.isAlive) _logoFadeTween.Stop();
            if (_logoPunchTween.isAlive) _logoPunchTween.Stop();
            if (_logoIdleTween.isAlive) _logoIdleTween.Stop();

            if (_logoImage == null) return;
            var rt = _logoImage.rectTransform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            if (_logoRestCaptured) rt.anchoredPosition = _logoRestPos;
            var c = _logoImage.color;
            c.a = 1f;
            _logoImage.color = c;
        }

        private static void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(clip, volume);
        }
    }

    /// <summary>
    /// Animación continua del logo mientras un tipo de tablero está activo. Cada
    /// estilo usa un canal distinto para no pelear con la transición: Pulse=scale
    /// (delay del punch), Bob=posición, Sway=rotación (comparte con los springs de
    /// Feel solo el estallido inicial).
    /// </summary>
    public enum LogoIdleStyle
    {
        None = 0,
        /// <summary>Latido de escala — agresivo, para Attack.</summary>
        Pulse = 1,
        /// <summary>Flote vertical calmo — para Defense.</summary>
        Bob = 2,
        /// <summary>Balanceo de rotación en Z.</summary>
        Sway = 3,
    }

    /// <summary>Config de un idle del logo: estilo + cuánto y cada cuánto.</summary>
    [System.Serializable]
    public struct LogoIdle
    {
        public LogoIdleStyle Style;

        [Tooltip("Pulse: delta de escala (0.08 = ±8%). Bob: píxeles de flote. Sway: grados en Z.")]
        public float Amplitude;

        [Tooltip("Duración del ciclo completo (ida y vuelta) en segundos.")]
        public float Period;
    }
}
