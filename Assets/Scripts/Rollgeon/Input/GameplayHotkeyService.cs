using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.GameCamera;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.Input
{
    /// <summary>
    /// Host del map "Gameplay" del <see cref="InputActionAsset"/> compartido. Lo
    /// habilita mientras el componente está activo y expone las actions a las views
    /// via <see cref="ServiceLocator"/> (<see cref="IGameplayHotkeyService"/>).
    /// Poné UNO en una escena persistente del gameplay (ej. el canvas raíz del HUD).
    /// Sigue el patrón de <see cref="CameraInputRouter"/>.
    /// </summary>
    [AddComponentMenu("Rollgeon/Input/Gameplay Hotkey Service")]
    public sealed class GameplayHotkeyService : MonoBehaviour, IGameplayHotkeyService
    {
        private const string LogPrefix = "[GameplayHotkeyService] ";

        [Title("Input")]
        [Tooltip("InputActionAsset con el map 'Gameplay'. Si queda null, se intenta " +
                 "resolver el mismo asset via CameraInputConfig (bootstrap de cámara).")]
        [SerializeField] private InputActionAsset _actions;

        [Tooltip("Nombre del action map. Default 'Gameplay'.")]
        [SerializeField] private string _mapName = "Gameplay";

        private InputActionMap _map;
        private readonly Dictionary<GameplayHotkey, InputAction> _byHotkey =
            new Dictionary<GameplayHotkey, InputAction>();

        public bool IsReady => _map != null;

        private void Awake()
        {
            // Registro temprano para que las views que bindean después (HUDs pusheados)
            // ya nos encuentren. Upsert: si hubiera otro host, gana el último.
            ServiceLocator.AddService<IGameplayHotkeyService>(this, ServiceScope.Global);
            Resolve();
        }

        private void OnEnable()
        {
            if (_map == null) Resolve();
            _map?.Enable();
        }

        private void OnDisable()
        {
            _map?.Disable();
        }

        private void OnDestroy()
        {
            // Solo desregistramos si seguimos siendo el host activo (evita pisar un
            // reemplazo que se haya registrado después).
            if (ServiceLocator.TryGetService<IGameplayHotkeyService>(out var svc)
                && ReferenceEquals(svc, this))
            {
                ServiceLocator.RemoveService<IGameplayHotkeyService>();
            }
        }

        private void Resolve()
        {
            if (_actions == null
                && ServiceLocator.TryGetService<CameraInputConfig>(out var cfg)
                && cfg != null)
            {
                // El proyecto usa UN solo InputSystem_Actions asset; el bootstrap de
                // cámara ya lo tiene registrado, así que sirve de fallback.
                _actions = cfg.Actions;
            }

            if (_actions == null)
            {
                Debug.LogWarning(LogPrefix + "InputActionAsset no asignado — hotkeys inertes.", this);
                return;
            }

            _map = _actions.FindActionMap(_mapName, throwIfNotFound: false);
            if (_map == null)
            {
                Debug.LogWarning(LogPrefix + $"No existe el map '{_mapName}' en '{_actions.name}'.", this);
                return;
            }

            _byHotkey.Clear();
            foreach (GameplayHotkey hotkey in Enum.GetValues(typeof(GameplayHotkey)))
            {
                var action = _map.FindAction(hotkey.ToString(), throwIfNotFound: false);
                if (action != null) _byHotkey[hotkey] = action;
                else Debug.LogWarning(LogPrefix + $"El map '{_mapName}' no tiene la action '{hotkey}'.", this);
            }
        }

        public void Subscribe(GameplayHotkey hotkey, Action<InputAction.CallbackContext> handler)
        {
            if (handler == null) return;
            if (_byHotkey.TryGetValue(hotkey, out var action) && action != null)
                action.performed += handler;
        }

        public void Unsubscribe(GameplayHotkey hotkey, Action<InputAction.CallbackContext> handler)
        {
            if (handler == null) return;
            if (_byHotkey.TryGetValue(hotkey, out var action) && action != null)
                action.performed -= handler;
        }

        public string GetKeyHint(GameplayHotkey hotkey)
        {
            if (!_byHotkey.TryGetValue(hotkey, out var action) || action == null)
                return string.Empty;

            try
            {
                // Con solo teclado hoy devuelve la tecla (ej. "Q", "Space"). Cuando
                // sumes gamepad, filtrá por control scheme acá (group mask).
                return action.GetBindingDisplayString(
                    InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
