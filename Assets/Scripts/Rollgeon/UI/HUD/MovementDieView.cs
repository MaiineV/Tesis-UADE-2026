using System;
using System.Collections;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Movement.Die;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Dado de Movimiento en la mesa de dados (§6.6). Vive centrado en el <c>RollArea</c> de
    /// <c>Canvas_ActionRoll</c> y está OCULTO salvo durante su tirada: la mesa se abre
    /// (<c>OnMovementDieRollStarted</c>), el dado gira en el centro, revela la cara, la deja
    /// leer un instante y se esconde; recién ahí la mesa se cierra (<c>OnMovementDieRolled</c>)
    /// y arranca la selección de tile.
    /// </summary>
    /// <remarks>
    /// Es el <see cref="IMovementDiePresenter"/> del <see cref="IMovementDieService"/>: el
    /// servicio ya conoce la cara, esta view solo la anima y avisa al terminar — el rango se
    /// publica recién en ese callback, así el hover preview no lo spoilea. Reusa
    /// <see cref="DiceSlotAnimator"/> (mismo spin y pacing por <c>GameSpeedPrefs</c> que los
    /// 5 dados de la build) y el <see cref="DiceUiAnimationSettingsSO"/> de Resources.
    /// Entidad visual separada del <see cref="DiceZoneView"/>: no toca sus 5 slots.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Movement Die View")]
    public sealed class MovementDieView : MonoBehaviour, IMovementDiePresenter
    {
        private const string LogPrefix = "[MovementDieView] ";
        private const string SettingsResourcePath = "Dice/DiceUiAnimationSettings";

        [Required("Arrastrar el DiceSlotView hijo (mismo prefab de slot que el DiceZoneView).")]
        [SerializeField] private DiceSlotView _slot;

        [SerializeField, Tooltip("Opcional: override del tuning de spin. Null = Resources/" + SettingsResourcePath + ".")]
        private DiceUiAnimationSettingsSO _animSettings;

        [SerializeField, MinValue(0f)]
        [Tooltip("Segundos que la cara queda visible tras el reveal antes de esconder el dado y " +
                 "cerrar la mesa. Sigue al game speed.")]
        private float _revealHoldSeconds = 0.6f;

        private DiceSlotAnimator _animator;
        private IMovementDieService _service;
        private Guid _playerGuid;
        private bool _bound;
        private Action _pendingReveal;
        private Coroutine _holdRoutine;

        // ---- Lifecycle ---------------------------------------------------------

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();
            _playerGuid = playerGuid;

            if (_slot == null)
            {
                Debug.LogWarning(LogPrefix + "_slot no cableado — el dado de Movimiento no se muestra.", this);
                return;
            }

            if (!ServiceLocator.TryGetService<IMovementDieService>(out _service) || _service == null)
            {
                // Sin servicio (escena vieja, tests) no hay dado que mostrar.
                Hide();
                return;
            }

            EnsureAnimator();
            _service.SetPresenter(this);
            _service.OnCleared += HandleCleared;
            _bound = true;
            Hide();
        }

        public void Unbind()
        {
            if (_service != null)
            {
                _service.OnCleared -= HandleCleared;
                _service.SetPresenter(null);
            }
            Abort();
            _service = null;
            _bound = false;
        }

        private void OnDestroy() => Unbind();

        // ---- IMovementDiePresenter ---------------------------------------------

        /// <inheritdoc />
        public bool TryPresent(DiceType type, int face, Action onRevealed)
        {
            if (!_bound || _slot == null || _animator == null || !gameObject.activeInHierarchy)
                return false;

            StopHold();
            _pendingReveal = onRevealed;
            _slot.gameObject.SetActive(true);
            _slot.Bind(type);
            _slot.SetSpinRole(DiceShapeRole.SideA);
            _slot.ClearSpinPreview();

            var settings = ResolveSettings();
            var plan = DiceAnimChoreographer.BuildSpinPlans(new[] { true }, settings.ToTimings())[0];
            _animator.PlaySpin(plan, face, _ =>
            {
                _slot.SetSpinRole(null);
                _slot.ShowFace(face);
                // Dejar leer la cara antes de esconder el dado y cerrar la mesa.
                StopHold();
                _holdRoutine = StartCoroutine(HoldThenReveal());
            });
            return true;
        }

        /// <inheritdoc />
        public void Abort()
        {
            _pendingReveal = null;
            StopHold();
            if (_animator != null) _animator.StopAll();
            if (_slot != null) _slot.SetSpinRole(null);
            Hide();
        }

        // ---- Internals ---------------------------------------------------------

        private IEnumerator HoldThenReveal()
        {
            float hold = _revealHoldSeconds / Rollgeon.Timing.GameSpeedPrefs.Multiplier;
            if (hold > 0f) yield return new WaitForSeconds(hold);
            _holdRoutine = null;
            var reveal = _pendingReveal;
            _pendingReveal = null;
            Hide();
            reveal?.Invoke();
        }

        private void StopHold()
        {
            if (_holdRoutine != null)
            {
                StopCoroutine(_holdRoutine);
                _holdRoutine = null;
            }
        }

        private void HandleCleared() => Hide();

        // Fuera de su tirada el dado no se ve: es una entidad propia, pero vive en la mesa
        // igual que los otros dados y solo aparece mientras se tira.
        private void Hide()
        {
            if (_slot != null) _slot.gameObject.SetActive(false);
        }

        private void EnsureAnimator()
        {
            if (_animator == null)
            {
                _animator = _slot.GetComponent<DiceSlotAnimator>();
                if (_animator == null) _animator = _slot.gameObject.AddComponent<DiceSlotAnimator>();
            }
            _animator.Init(_slot, ResolveSettings());
        }

        private DiceUiAnimationSettingsSO ResolveSettings()
        {
            if (_animSettings != null) return _animSettings;
            _animSettings = Resources.Load<DiceUiAnimationSettingsSO>(SettingsResourcePath);
            if (_animSettings == null)
                _animSettings = ScriptableObject.CreateInstance<DiceUiAnimationSettingsSO>();
            return _animSettings;
        }
    }
}
