using Patterns;
using Rollgeon.GameCamera;
using UnityEngine.InputSystem;

namespace Rollgeon.Dice.Throw
{
    /// <summary>
    /// Suprime el action map "Camera" mientras dura una sesión de throw: su binding de
    /// rightButton (RotateModifier) chocaría con el click derecho de cancelar el agarre.
    /// Restore garantizado vía <see cref="Release"/> (el presenter lo llama en el cierre
    /// de sesión, en el abort y en OnDisable).
    /// </summary>
    public sealed class DiceThrowInputScope
    {
        private InputActionMap _cameraMap;
        private bool _wasEnabled;
        private bool _held;

        public void Acquire()
        {
            if (_held) return;
            _held = true;

            _cameraMap = ResolveCameraMap();
            _wasEnabled = _cameraMap is { enabled: true };
            if (_wasEnabled) _cameraMap.Disable();
        }

        public void Release()
        {
            if (!_held) return;
            _held = false;

            if (_wasEnabled && _cameraMap != null) _cameraMap.Enable();
            _cameraMap = null;
            _wasEnabled = false;
        }

        // Mismo fallback de resolución del asset que GameplayHotkeyService.
        private static InputActionMap ResolveCameraMap()
        {
            if (!ServiceLocator.TryGetService<CameraInputConfig>(out var cfg)
                || cfg == null || cfg.Actions == null)
                return null;
            return cfg.Actions.FindActionMap("Camera", throwIfNotFound: false);
        }
    }
}
