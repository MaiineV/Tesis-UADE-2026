using Patterns;
using Rollgeon.Patterns.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rollgeon.GameCamera
{
    /// <summary>
    /// Wrapper opcional <see cref="IPreloadableService"/> que registra el
    /// <see cref="CameraConfigSO"/> en <see cref="ServiceLocator"/>
    /// (<see cref="ServiceScope.Global"/>) y, si se le pasa un
    /// <see cref="InputActionAsset"/>, registra un <see cref="CameraInputConfig"/>
    /// consultable por el <see cref="CameraInputRouter"/> al montarse en la
    /// scene de gameplay.
    /// </summary>
    /// <remarks>
    /// Alternativamente, el diseñador puede dropear el <c>CameraConfig.asset</c>
    /// directo en <c>ServiceBootstrapSO.SettingsAssets</c>; este wrapper sólo
    /// es necesario si además querés bootstrap-wirear el <c>InputActionAsset</c>.
    /// Priority 45 — antes de los Run-scope services (75+) porque el config
    /// vive en scope Global.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Camera/Camera Service Bootstrap", fileName = "CameraServiceBootstrap")]
    public sealed class CameraServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private const string LogPrefix = "[CameraServiceBootstrap] ";

        [Title("Config")]
        [Required, Tooltip("CameraConfigSO con el tuning (rotate, pan, zoom, wall occlusion, floor view).")]
        [SerializeField] private CameraConfigSO _config;

        [Title("Input (opcional)")]
        [Tooltip("InputActionAsset que contiene el map 'Camera'. Si se setea, el CameraInputRouter " +
                 "en la scene de gameplay lo consume via CameraInputConfig.")]
        [SerializeField] private InputActionAsset _inputActions;

        [Tooltip("Nombre del action map dentro del asset. Default = 'Camera'.")]
        [SerializeField] private string _mapName = "Camera";

        public int Priority => 45;

        public void Register()
        {
            if (_config == null)
            {
                Debug.LogError(LogPrefix + "CameraConfigSO no asignado — camera config no se registra.");
                return;
            }

            ServiceLocator.AddService<CameraConfigSO>(_config, ServiceScope.Global);

            if (_inputActions != null)
            {
                ServiceLocator.AddService<CameraInputConfig>(
                    new CameraInputConfig(_inputActions, _mapName),
                    ServiceScope.Global);
            }

            Debug.Log(LogPrefix + $"Registered CameraConfig='{_config.name}'" +
                      (_inputActions != null ? $" + InputActions='{_inputActions.name}'" : ""));
        }
    }

    /// <summary>
    /// Ligero POCO para pasar el <see cref="InputActionAsset"/> + map name al
    /// <see cref="CameraInputRouter"/> via <see cref="ServiceLocator"/>. No es
    /// un ScriptableObject porque no necesita editor-authorship.
    /// </summary>
    public sealed class CameraInputConfig
    {
        public InputActionAsset Actions { get; }
        public string MapName { get; }

        public CameraInputConfig(InputActionAsset actions, string mapName)
        {
            Actions = actions;
            MapName = mapName;
        }
    }
}
