using System.Collections;
using MoreMountains.Feedbacks;
using Patterns;
using Rollgeon.Audio;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Juice de zona para los dados legacy: shakes de la mesa (RollArea), flourish de
    /// combo, pulso de "kept" en rerolls, y TODO el audio de dados. El audio se
    /// centraliza acá (y no en <see cref="DiceSlotJuice"/>) por dos razones: la
    /// convención de <c>IAudioService</c> (TECHNICAL.md §17 — nada de MMF_Sound con
    /// AudioSources propios) y los pitch ramps, que necesitan estado de zona (orden
    /// de reveal, cantidad de holds). Todos los campos son opcionales: sin wiring, no-op.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Dice Zone Juice")]
    public sealed class DiceZoneJuice : MonoBehaviour
    {
        [Title("Players de zona (hijo ZoneJuice)")]
        [SerializeField, Optional, Tooltip("Pre-shake sutil de la RollArea al arrancar el throw (≤3 px).")]
        private MMF_Player _throwPreShakePlayer;

        [SerializeField, Optional, Tooltip("Micro-shake de la mesa cuando los dados aterrizan (fin del outro, ≤6 px).")]
        private MMF_Player _outroLandPlayer;

        [SerializeField, Optional, Tooltip("Flourish visual al matchear un combo nuevo (además de los bumps por dado).")]
        private MMF_Player _comboFlourishPlayer;

        [Title("SFX (via IAudioService, 2D)")]
        [SerializeField, Optional, Tooltip("Rattle mientras los dados giran (≈ duración del spin).")]
        private AudioClip _spinRattleClip;

        [SerializeField, Optional, Tooltip("Tick por cada dado que revela — el pitch sube con el orden.")]
        private AudioClip _revealTickClip;

        [SerializeField, Optional, Tooltip("Click al holdear — el pitch se apila con la cantidad holdeada.")]
        private AudioClip _lockClip;

        [SerializeField, Optional, Tooltip("Release suave al des-holdear.")]
        private AudioClip _unlockClip;

        [SerializeField, Optional, Tooltip("Whoosh al lanzar los dados al centro (1 por confirm).")]
        private AudioClip _throwWhooshClip;

        [SerializeField, Optional, Tooltip("Poof de los dados descartados (1 por confirm).")]
        private AudioClip _discardPoofClip;

        [SerializeField, Optional, Tooltip("Thud al aterrizar (fin del outro). Placeholder: SE-Collision_03.")]
        private AudioClip _landThudClip;

        [SerializeField, Optional, Tooltip("Chime al matchear un combo nuevo.")]
        private AudioClip _comboChimeClip;

        [Title("Tuning")]
        [SerializeField, Tooltip("Incremento de pitch por orden de reveal (1º=1.0, 2º=1.06...).")]
        private float _revealPitchStep = 0.06f;

        [SerializeField, Tooltip("Incremento de pitch por dado ya holdeado al lockear.")]
        private float _lockPitchStep = 0.04f;

        [SerializeField, Tooltip("Stagger entre bumps de dados holdeados en el flourish de combo.")]
        private float _comboPulseStagger = 0.05f;

        private DiceZoneAnimator _animator;
        private bool _slotsHooked;
        private string _lastComboId = string.Empty;
        private int _lastDiscardFrame = -1;
        private System.Action<ComboMatchedPayload> _onComboMatched;

        private void OnEnable()
        {
            _onComboMatched = HandleComboMatched;
            TypedEvent<ComboMatchedPayload>.Subscribe(_onComboMatched);
            TryHook();
        }

        private void Update()
        {
            // El DiceZoneAnimator lo agrega DiceZoneView.Bind por código — reintento
            // hasta engancharlo (mismo patrón que ActionRollExplorationVisibility).
            if (_animator == null || !_slotsHooked) TryHook();
        }

        private void TryHook()
        {
            if (_animator == null)
            {
                _animator = GetComponent<DiceZoneAnimator>();
                if (_animator == null) return;
                _animator.SpinSessionStarted += HandleSpinSession;
                _animator.FaceRevealOrdered += HandleFaceRevealOrdered;
                _animator.ZoneThrowStarted += HandleZoneThrow;
                _animator.OutroFinished += HandleOutroFinished;
            }
            if (!_slotsHooked && _animator.SlotCount > 0)
            {
                for (int i = 0; i < _animator.SlotCount; i++)
                {
                    var slot = _animator.GetSlotAnimator(i);
                    if (slot == null) continue;
                    slot.DieLocked += HandleDieLocked;
                    slot.DieUnlocked += HandleDieUnlocked;
                    slot.DieDiscarded += HandleDieDiscarded;
                }
                _slotsHooked = true;
            }
        }

        private void OnDisable()
        {
            if (_onComboMatched != null)
            {
                TypedEvent<ComboMatchedPayload>.Unsubscribe(_onComboMatched);
                _onComboMatched = null;
            }
            if (_animator != null)
            {
                _animator.SpinSessionStarted -= HandleSpinSession;
                _animator.FaceRevealOrdered -= HandleFaceRevealOrdered;
                _animator.ZoneThrowStarted -= HandleZoneThrow;
                _animator.OutroFinished -= HandleOutroFinished;
                if (_slotsHooked)
                {
                    for (int i = 0; i < _animator.SlotCount; i++)
                    {
                        var slot = _animator.GetSlotAnimator(i);
                        if (slot == null) continue;
                        slot.DieLocked -= HandleDieLocked;
                        slot.DieUnlocked -= HandleDieUnlocked;
                        slot.DieDiscarded -= HandleDieDiscarded;
                    }
                }
                _animator = null;
            }
            _slotsHooked = false;
            _lastComboId = string.Empty;
        }

        // ---- Handlers ----------------------------------------------------------

        private void HandleSpinSession()
        {
            PlaySfx(_spinRattleClip, volume: 0.7f, pitch: Random.Range(0.95f, 1.05f));

            // Reroll (hay dados holdeados que se quedan): pulso de reaseguro en cada
            // uno mientras los demás giran.
            if (_animator == null || _animator.RaisedCount == 0) return;
            for (int i = 0; i < _animator.SlotCount; i++)
            {
                var slot = _animator.GetSlotAnimator(i);
                if (slot == null || !slot.IsRaised) continue;
                slot.GetComponent<DiceSlotJuice>()?.PlayKeptPulse();
            }
        }

        private void HandleFaceRevealOrdered(int slotIndex, int revealOrder)
        {
            PlaySfx(_revealTickClip, volume: 0.8f, pitch: 1f + _revealPitchStep * revealOrder);
        }

        private void HandleDieLocked()
        {
            // RaisedCount ya incluye el dado recién lockeado — el 1º suena a 1.0.
            int stacked = _animator != null ? Mathf.Max(0, _animator.RaisedCount - 1) : 0;
            PlaySfx(_lockClip, volume: 0.8f, pitch: 1f + _lockPitchStep * stacked);
        }

        private void HandleDieUnlocked()
        {
            PlaySfx(_unlockClip, volume: 0.6f, pitch: 0.9f);
        }

        private void HandleDieDiscarded()
        {
            // Los descartes arrancan todos el mismo frame — un solo poof, no un coro.
            if (Time.frameCount == _lastDiscardFrame) return;
            _lastDiscardFrame = Time.frameCount;
            PlaySfx(_discardPoofClip, volume: 0.5f);
        }

        private void HandleZoneThrow()
        {
            Play(_throwPreShakePlayer);
            PlaySfx(_throwWhooshClip, volume: 0.9f, isImportant: true);
        }

        private void HandleOutroFinished()
        {
            Play(_outroLandPlayer);
            PlaySfx(_landThudClip, volume: 0.8f, isImportant: true);
        }

        private void HandleComboMatched(ComboMatchedPayload payload)
        {
            // Dispara solo cuando el combo CAMBIA a uno nuevo — el payload llega en
            // cada toggle de hold y repetir el flourish sería spam.
            string comboId = payload.ComboId ?? string.Empty;
            if (comboId == _lastComboId) return;
            _lastComboId = comboId;
            if (comboId.Length == 0) return;

            Play(_comboFlourishPlayer);
            PlaySfx(_comboChimeClip, volume: 0.8f, isImportant: true);
            if (isActiveAndEnabled && _animator != null)
                StartCoroutine(ComboPulseRoutine());
        }

        // Bumps escalonados sobre los dados holdeados: "estos hicieron el combo".
        private IEnumerator ComboPulseRoutine()
        {
            for (int i = 0; i < _animator.SlotCount; i++)
            {
                var slot = _animator.GetSlotAnimator(i);
                if (slot == null || !slot.IsRaised) continue;
                slot.GetComponent<DiceSlotJuice>()?.PlayKeptPulse();
                if (_comboPulseStagger > 0f)
                    yield return new WaitForSeconds(_comboPulseStagger);
            }
        }

        // ---- Helpers -----------------------------------------------------------

        private static void Play(MMF_Player player)
        {
            if (player == null) return;
            if (player.IsPlaying) player.StopFeedbacks();
            player.PlayFeedbacks();
        }

        private static void PlaySfx(AudioClip clip, float volume = 1f, float pitch = 1f, bool isImportant = false)
        {
            if (clip == null) return;
            if (ServiceLocator.TryGetService<IAudioService>(out var audio) && audio != null)
                audio.PlaySfx2D(clip, volume, pitch, isImportant);
        }
    }
}
