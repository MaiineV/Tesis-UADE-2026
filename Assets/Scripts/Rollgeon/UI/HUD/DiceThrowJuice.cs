using Patterns;
using Rollgeon.Audio;
using Rollgeon.Dice.Throw;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Juice de zona de los dados arrojables: TODO el audio de los modos 2D/3D
    /// (via <c>IAudioService</c>, convención §17), el hitstop del crit y el duck de
    /// música durante la sesión. Se suscribe a los eventos POR ÍNDICE de los
    /// presenters (payload plano — los ghosts mueren el mismo frame del reveal) y a
    /// los eventos del <see cref="IDiceThrowService"/>, que es Run-scoped: se
    /// re-resuelve por polling como hacen los presenters. Campos opcionales: sin
    /// wiring, no-op.
    /// </summary>
    /// <remarks>
    /// El rattle de la mano son one-shots re-agendados por velocidad del cursor
    /// (no hay API de loop en IAudioService) con <c>Time.unscaledTime</c> — el
    /// hitstop congela timeScale y un scheduler escalado se congelaría con él.
    /// Los rebotes del mismo frame se coalescen en UN clack más fuerte: cinco
    /// dados contra una esquina en fase suenan a comb filter, no a dados.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Throw Juice")]
    public sealed class DiceThrowJuice : MonoBehaviour
    {
        [Title("Presenters")]
        [SerializeField, Optional]
        private DiceThrow2DPresenter _presenter2D;

        [Title("SFX (2D)")]
        [SerializeField, Optional, Tooltip("Al agarrar un dado (sesión o arming de reroll).")]
        private AudioClip _pickupClip;

        [SerializeField, Optional, Tooltip("Clack de rebote contra bordes (volumen/pitch por velocidad).")]
        private AudioClip _bounceClip;

        [SerializeField, Optional, Tooltip("Tick al asentarse cada dado (pitch ramp por orden).")]
        private AudioClip _settleTickClip;

        [SerializeField, Optional, Tooltip("Whoosh del flick.")]
        private AudioClip _whooshClip;

        [SerializeField, Optional, Tooltip("Cancel del agarre (RMB / soltar suave).")]
        private AudioClip _cancelClip;

        [SerializeField, Optional, Tooltip("Rattle de dados en la mano (one-shots re-agendados).")]
        private AudioClip _rattleClip;

        [Title("Tuning")]
        [SerializeField, Tooltip("Velocidad de referencia (px/s) para volumen/pitch de impactos.")]
        private float _impactRefSpeed = 1500f;

        [SerializeField, Tooltip("Rebotes por debajo de esta velocidad no suenan (dado moribundo).")]
        private float _bounceMinSpeed = 120f;

        [SerializeField, Tooltip("Paso de pitch por orden de settle (ramp ascendente).")]
        private float _settlePitchStep = 0.06f;

        [SerializeField, Tooltip("Velocidad de cursor (px/s) desde la que la mano 'rattlea'.")]
        private float _rattleMinSpeed = 250f;

        [SerializeField, Tooltip("Velocidad de referencia del rattle (px/s) — más rápido = más denso.")]
        private float _rattleRefSpeed = 1500f;

        [Title("Crit / Duck")]
        [SerializeField, Tooltip("Cara desde la que el settle cuenta como crit (hitstop).")]
        private int _critFace = 6;

        [SerializeField, Range(0f, 0.25f)]
        private float _critHitstopSeconds = 0.05f;

        [SerializeField, Range(0.3f, 1f), Tooltip("Factor de duck de música durante la sesión de throw.")]
        private float _musicDuckFactor = 0.85f;

        private IDiceThrowService _service;
        private bool _ducked;

        private float _nextRattleAt;
        private float _nextClackAt;
        private float _pendingBounceSpeed;
        private int _pendingBounceCount;
        private float _nextPickupAt;

        // ---- Lifecycle ---------------------------------------------------------

        private void OnEnable()
        {
            if (_presenter2D != null)
            {
                _presenter2D.DieGrabbed += HandleDieGrabbed;
                _presenter2D.DiceThrown += HandleDiceThrown;
                _presenter2D.DieBounced += HandleDieBounced;
                _presenter2D.DieSettled += HandleDieSettled;
            }
        }

        private void OnDisable()
        {
            if (_presenter2D != null)
            {
                _presenter2D.DieGrabbed -= HandleDieGrabbed;
                _presenter2D.DiceThrown -= HandleDiceThrown;
                _presenter2D.DieBounced -= HandleDieBounced;
                _presenter2D.DieSettled -= HandleDieSettled;
            }
            UnhookService();
            // AudioManager es DontDestroyOnLoad: un duck colgado sobrevive la escena.
            RestoreDuck();
        }

        private void Update()
        {
            SyncServiceBinding();
            TickRattle();
        }

        private void LateUpdate()
        {
            FlushBounceClack();
        }

        // ---- Service (Run-scoped — polling como los presenters) -----------------

        private void SyncServiceBinding()
        {
            ServiceLocator.TryGetService<IDiceThrowService>(out var svc);
            if (ReferenceEquals(svc, _service)) return;

            UnhookService();
            _service = svc;
            if (_service == null) return;
            _service.OnSessionStarted += HandleSessionStarted;
            _service.OnPhaseChanged += HandlePhaseChanged;
            _service.OnGrabCancelled += HandleGrabCancelled;
            _service.OnSessionAborted += HandleSessionAborted;
        }

        private void UnhookService()
        {
            if (_service == null) return;
            _service.OnSessionStarted -= HandleSessionStarted;
            _service.OnPhaseChanged -= HandlePhaseChanged;
            _service.OnGrabCancelled -= HandleGrabCancelled;
            _service.OnSessionAborted -= HandleSessionAborted;
            _service = null;
        }

        private void HandleSessionStarted()
        {
            DuckMusic(_musicDuckFactor, 0.2f);
            _ducked = true;
        }

        private void HandlePhaseChanged(DiceThrowPhase phase)
        {
            // El reveal no tiene evento propio: CompleteReveal limpia la sesión y la
            // fase vuelve a Idle — ese es el momento de soltar el duck.
            if (phase == DiceThrowPhase.Idle) RestoreDuck();
        }

        private void HandleGrabCancelled()
        {
            PlaySfx(_cancelClip, volume: 0.5f, pitch: 0.9f);
        }

        private void HandleSessionAborted() => RestoreDuck();

        private void RestoreDuck()
        {
            if (!_ducked) return;
            _ducked = false;
            DuckMusic(1f, 0.35f);
        }

        // ---- Momentos por dado (2D) ---------------------------------------------

        private void HandleDieGrabbed(int index)
        {
            // El grab acumulativo puede tragarse varios dados en frames seguidos —
            // un mínimo entre pops evita la ametralladora.
            if (Time.unscaledTime < _nextPickupAt) return;
            _nextPickupAt = Time.unscaledTime + 0.04f;
            PlaySfx(_pickupClip, volume: 0.55f, pitch: Random.Range(0.95f, 1.1f));
        }

        private void HandleDiceThrown(int count)
        {
            PlaySfx(_whooshClip, volume: 0.9f, isImportant: true);
        }

        private void HandleDieBounced(int index, float speed)
        {
            if (speed < _bounceMinSpeed) return;
            _pendingBounceCount++;
            _pendingBounceSpeed = Mathf.Max(_pendingBounceSpeed, speed);
        }

        private void FlushBounceClack()
        {
            if (_pendingBounceCount == 0) return;
            if (Time.unscaledTime >= _nextClackAt)
            {
                _nextClackAt = Time.unscaledTime + 0.05f;
                float vol = DiceThrowFeelMath.ImpactVolume(_pendingBounceSpeed, _impactRefSpeed)
                            * Mathf.Min(1.3f, 1f + 0.15f * (_pendingBounceCount - 1));
                float pitch = DiceThrowFeelMath.ImpactPitch(_pendingBounceSpeed, _impactRefSpeed)
                              * Random.Range(0.96f, 1.04f);
                PlaySfx(_bounceClip, Mathf.Min(1f, vol), pitch);
            }
            _pendingBounceCount = 0;
            _pendingBounceSpeed = 0f;
        }

        private void HandleDieSettled(int index, int face, int order)
        {
            PlaySfx(_settleTickClip, volume: 0.8f, pitch: 1f + _settlePitchStep * order);
            // Hitstop = política de zona (congelar el juego no es del dado individual).
            if (face >= _critFace && !DiceUiMotionPrefs.ReducedMotion)
                DiceHitstop.Play(_critHitstopSeconds);
        }

        // ---- Rattle de la mano -----------------------------------------------------

        private void TickRattle()
        {
            if (_rattleClip == null || _presenter2D == null) return;
            if (!_presenter2D.IsCarryingDice) return;

            float speed = _presenter2D.SmoothedCursorVelocity.magnitude;
            if (speed < _rattleMinSpeed) return;
            if (Time.unscaledTime < _nextRattleAt) return;

            float t = DiceThrowFeelMath.Intensity01(speed, _rattleRefSpeed);
            PlaySfx(_rattleClip, volume: Mathf.Lerp(0.25f, 0.6f, t),
                pitch: Random.Range(0.92f, 1.12f));
            _nextRattleAt = Time.unscaledTime
                            + DiceThrowFeelMath.RattleInterval(speed, _rattleRefSpeed);
        }

        // ---- Helpers -----------------------------------------------------------------

        private static void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f, bool isImportant = false)
        {
            if (clip == null) return;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(clip, volume, pitch, isImportant);
        }

        private static void DuckMusic(float factor, float fadeSeconds)
        {
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.DuckMusic(factor, fadeSeconds);
        }
    }
}
