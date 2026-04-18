using System.Collections.Generic;
using Patterns;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI
{
    /// <summary>
    /// MonoBehaviour por escena que descubre las <see cref="BaseScreen"/> hijas, las registra
    /// en el <see cref="IScreenManager"/>, y pushea la initial screen configurada en Inspector.
    /// Plan §4.5.
    /// </summary>
    /// <remarks>
    /// - Instancia una nueva <see cref="ScreenManager"/> en <c>Awake</c> y la registra en
    ///   <see cref="ServiceLocator"/> con scope <see cref="ServiceScope.Global"/>.
    /// - Desactiva todas las screens encontradas antes de pushear la inicial — asegura que solo
    ///   la pusheada este visible.
    /// - En <c>OnDestroy</c> deshace el registro y remueve el servicio.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screen Host")]
    public class ScreenHost : MonoBehaviour
    {
        private const string LogPrefix = "[ScreenHost] ";

        [Title("Screen Host")]
        [Tooltip("ScreenStringId de la screen que se pushea automaticamente en Awake. " +
                 "Vacio = solo registra, no pushea. Default para 01_MainMenu: 'MainMenu'.")]
        [SerializeField]
        private string _initialScreenStringId = "MainMenu";

        [Tooltip("Si esta activo, busca BaseScreen tambien entre GameObjects inactivos al arrancar.")]
        [SerializeField]
        private bool _includeInactive = true;

        private readonly List<IBaseScreen> _registered = new List<IBaseScreen>();
        private IScreenManager _manager;

        private void Awake()
        {
            _manager = new ScreenManager();
            ServiceLocator.AddService<IScreenManager>(_manager, ServiceScope.Global);

            var screens = GetComponentsInChildren<BaseScreen>(_includeInactive);
            if (screens == null || screens.Length == 0)
            {
                Debug.LogWarning(LogPrefix + "No BaseScreen children found. Esperado al menos una " +
                                 "(ver docs/setup/UI#0102_MainMenu.md §8.3).", this);
            }

            foreach (var screen in screens)
            {
                _manager.RegisterScreen(screen);
                _registered.Add(screen);
                screen.gameObject.SetActive(false);
            }

            if (!string.IsNullOrEmpty(_initialScreenStringId))
            {
                _manager.PushByStringId(_initialScreenStringId);
            }
            else
            {
                Debug.Log(LogPrefix + "InitialScreenStringId vacio — solo registro, no push inicial.", this);
            }
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                foreach (var screen in _registered)
                {
                    _manager.UnregisterScreen(screen);
                }
                _registered.Clear();
            }

            // Solo remover el servicio si la instancia registrada es la nuestra (defensivo frente a
            // escenas con varios ScreenHost en cascada o hot-reload del editor).
            if (ServiceLocator.TryGetService<IScreenManager>(out var current) && ReferenceEquals(current, _manager))
            {
                ServiceLocator.RemoveService<IScreenManager>();
            }

            _manager = null;
        }
    }
}
