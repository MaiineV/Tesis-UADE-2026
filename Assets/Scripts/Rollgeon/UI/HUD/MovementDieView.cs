using System;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Movement.Die;
using Rollgeon.UI.HUD.DiceAnim;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Slot del dado de Movimiento en el HUD de combate (§6.6). Entidad visual separada del
    /// <see cref="DiceZoneView"/>: un único <see cref="DiceSlotView"/> que muestra el tipo del
    /// dado de la clase y, al resolver Movimiento, gira y revela la cara (= rango).
    /// </summary>
    /// <remarks>
    /// Es el <see cref="IMovementDiePresenter"/> del <see cref="IMovementDieService"/>: el
    /// servicio ya conoce la cara, esta view solo la anima y avisa al terminar — el rango se
    /// publica recién en ese callback, así el hover preview no lo spoilea. Reusa
    /// <see cref="DiceSlotAnimator"/> (mismo spin, mismo pacing por <c>GameSpeedPrefs</c>) y
    /// el <see cref="DiceUiAnimationSettingsSO"/> de Resources del DiceZoneView.
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

        [SerializeField, Tooltip("Ocultar el slot fuera de combate (Bind/Unbind lo prende/apaga).")]
        private bool _hideWhenUnbound = true;

        private DiceSlotAnimator _animator;
        private IMovementDieService _service;
        private Guid _playerGuid;
        private bool _bound;
        private Action _pendingReveal;

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
                gameObject.SetActive(false);
                return;
            }

            EnsureAnimator();
            _service.SetPresenter(this);
            _service.OnCleared += HandleCleared;
            _bound = true;

            gameObject.SetActive(true);
            ShowIdle();
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
            if (_hideWhenUnbound) gameObject.SetActive(false);
        }

        private void OnDestroy() => Unbind();

        // ---- IMovementDiePresenter ---------------------------------------------

        /// <inheritdoc />
        public bool TryPresent(DiceType type, int face, Action onRevealed)
        {
            if (!_bound || _slot == null || _animator == null || !gameObject.activeInHierarchy)
                return false;

            _pendingReveal = onRevealed;
            _slot.SetDiceType(type);
            _slot.SetSpinRole(DiceShapeRole.SideA);
            _slot.ClearSpinPreview();

            var settings = ResolveSettings();
            var plan = DiceAnimChoreographer.BuildSpinPlans(new[] { true }, settings.ToTimings())[0];
            _animator.PlaySpin(plan, face, _ =>
            {
                _slot.SetSpinRole(null);
                _slot.ShowFace(face);
                var reveal = _pendingReveal;
                _pendingReveal = null;
                reveal?.Invoke();
            });
            return true;
        }

        /// <inheritdoc />
        public void Abort()
        {
            _pendingReveal = null;
            if (_animator != null) _animator.StopAll();
            if (_slot != null) _slot.SetSpinRole(null);
            ShowIdle();
        }

        // ---- Internals ---------------------------------------------------------

        private void HandleCleared() => ShowIdle();

        // Sin tirada vigente el slot muestra el tipo (D4…) sin número: el dado "existe"
        // como entidad propia aunque no se esté moviendo.
        private void ShowIdle()
        {
            if (_slot == null) return;
            var type = _service != null ? _service.CurrentType : MovementDieSO.DefaultType;
            _slot.Bind(type);
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
