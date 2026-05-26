using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.GameCamera;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Toggle de visibilidad entre la sala instanciada (world-prefab) y los
    /// shells procedurales del piso completo (§17.E.9). Registrado en
    /// <see cref="ServiceScope.Run"/> por <see cref="CreateAndRegister"/>.
    /// <para>
    /// Suscribe <see cref="EventName.OnCameraFloorViewToggled"/> para pasar
    /// entre modos (sala visible / shells visibles) y
    /// <see cref="EventName.OnRoomEntered"/> para refrescar el target de
    /// "sala actual" cuando el player cambia de habitación.
    /// </para>
    /// <para>
    /// Los shell GameObjects se materializan lazy la primera vez que floor
    /// view se activa — evita crear assets GameObject en tests EditMode que
    /// no tocan la cámara. La metadata viene de
    /// <see cref="IDungeonService.GetFloorShells"/>.
    /// </para>
    /// </summary>
    public sealed class FloorShellVisibilityController : IDisposable
    {
        private const string LogPrefix = "[FloorShellVisibilityController] ";

        private readonly IDungeonService _dungeon;
        private readonly CameraConfigSO _config;

        private readonly Dictionary<Guid, GameObject> _shellGOs = new();
        private Transform _shellRoot;
        private Material _sharedShellMaterial;

        private EventManager.EventReceiver _onFloorViewToggled;
        private EventManager.EventReceiver _onRoomEntered;

        private bool _isFloorView;
        private bool _disposed;

        public FloorShellVisibilityController(IDungeonService dungeon, CameraConfigSO config)
        {
            _dungeon = dungeon ?? throw new ArgumentNullException(nameof(dungeon));
            _config = config;

            _onFloorViewToggled = OnFloorViewToggled;
            _onRoomEntered = OnRoomEntered;
            EventManager.Subscribe(EventName.OnCameraFloorViewToggled, _onFloorViewToggled);
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEntered);
        }

        /// <summary>
        /// Factory Run-scope: resuelve deps via <see cref="ServiceLocator"/>.
        /// Si <see cref="IDungeonService"/> no está, retorna <c>null</c> (log).
        /// </summary>
        public static FloorShellVisibilityController CreateAndRegister()
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                Debug.LogWarning(LogPrefix + "IDungeonService no registrado — no se creó.");
                return null;
            }
            ServiceLocator.TryGetService<CameraConfigSO>(out var config);

            var ctrl = new FloorShellVisibilityController(dungeon, config);
            ServiceLocator.AddService<FloorShellVisibilityController>(ctrl, ServiceScope.Run);
            return ctrl;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onFloorViewToggled != null)
            {
                EventManager.UnSubscribe(EventName.OnCameraFloorViewToggled, _onFloorViewToggled);
                _onFloorViewToggled = null;
            }
            if (_onRoomEntered != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEntered);
                _onRoomEntered = null;
            }

            foreach (var go in _shellGOs.Values)
            {
                if (go != null) UnityEngine.Object.Destroy(go);
            }
            _shellGOs.Clear();

            if (_shellRoot != null)
            {
                UnityEngine.Object.Destroy(_shellRoot.gameObject);
                _shellRoot = null;
            }
            if (_sharedShellMaterial != null && (_config == null || _config.ShellMaterial != _sharedShellMaterial))
            {
                UnityEngine.Object.Destroy(_sharedShellMaterial);
            }
            _sharedShellMaterial = null;
        }

        private void OnFloorViewToggled(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (args[0] is not bool on) return;

            _isFloorView = on;
            ApplyVisibility();
        }

        private void OnRoomEntered(params object[] args)
        {
            if (!_isFloorView) return;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            MaterializeShellsIfNeeded();

            var current = _dungeon.CurrentRoomInstance;
            foreach (var (id, go) in _shellGOs)
            {
                if (go == null) continue;
                bool isCurrent = current != null && id == current.InstanceId;
                go.SetActive(_isFloorView && !isCurrent);
            }
        }

        private void MaterializeShellsIfNeeded()
        {
            if (_shellGOs.Count > 0) return;

            var shells = _dungeon.GetFloorShells();
            if (shells == null || shells.Count == 0) return;

            if (_shellRoot == null)
            {
                var rootGO = new GameObject("FloorShells");
                _shellRoot = rootGO.transform;
            }

            if (_config != null && _config.ShellMaterial != null)
            {
                _sharedShellMaterial = _config.ShellMaterial;
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    Debug.LogWarning(LogPrefix + "URP Unlit shader not found, falling back to Standard.");
                    shader = Shader.Find("Standard");
                }
                _sharedShellMaterial = new Material(shader);
                Color shellColor = _config != null ? _config.ShellColor : new Color(0.1f, 0.1f, 0.15f, 0.85f);
                _sharedShellMaterial.color = shellColor;
            }

            foreach (var (id, shell) in shells)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Shell_{id:N}";
                cube.transform.SetParent(_shellRoot, worldPositionStays: false);
                cube.transform.position = shell.WorldPosition;
                cube.transform.localScale = shell.Size;

                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = _sharedShellMaterial;

                var collider = cube.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);

                cube.SetActive(false);
                _shellGOs[id] = cube;
            }
        }
    }
}
