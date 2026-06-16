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

        // Ícono de sala especial: tamaño world y cuánto flota sobre la cara superior del shell.
        private const float IconWorldSize = 3f;
        private const float IconHeightOffset = 0.75f;

        private readonly IDungeonService _dungeon;
        private readonly CameraConfigSO _config;

        private readonly Dictionary<Guid, GameObject> _shellGOs = new();
        private readonly Dictionary<Guid, GameObject> _shellIcons = new();
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
                if (go != null) DestroyObject(go);
            }
            _shellGOs.Clear();

            foreach (var icon in _shellIcons.Values)
            {
                if (icon != null) DestroyObject(icon);
            }
            _shellIcons.Clear();

            if (_shellRoot != null)
            {
                DestroyObject(_shellRoot.gameObject);
                _shellRoot = null;
            }
            if (_sharedShellMaterial != null && (_config == null || _config.ShellMaterial != _sharedShellMaterial))
            {
                DestroyObject(_sharedShellMaterial);
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
            var rooms = _dungeon.GetAllRoomInstances();
            foreach (var (id, go) in _shellGOs)
            {
                if (go == null) continue;
                bool isCurrent = current != null && id == current.InstanceId;
                // Fog of war (#158): solo salas descubiertas (visitadas o vecinas conectadas
                // a una visitada). La sala actual se muestra como prefab world, no como shell.
                bool visible = _isFloorView && !isCurrent && IsDiscovered(id, rooms);
                go.SetActive(visible);
                if (_shellIcons.TryGetValue(id, out var icon) && icon != null)
                    icon.SetActive(visible);
            }
        }

        /// <summary>
        /// Descubierta = visitada O vecina conectada por puerta a una sala visitada.
        /// La adyacencia sale de <see cref="RoomInstance.Connections"/> (solo conexiones reales).
        /// </summary>
        private static bool IsDiscovered(Guid id, IReadOnlyDictionary<Guid, RoomInstance> rooms)
        {
            if (rooms == null || !rooms.TryGetValue(id, out var room) || room == null) return false;
            if (room.Visited) return true;
            foreach (var neighborId in room.Connections.Values)
                if (rooms.TryGetValue(neighborId, out var neighbor) && neighbor != null && neighbor.Visited)
                    return true;
            return false;
        }

        private void MaterializeShellsIfNeeded()
        {
            var shells = _dungeon.GetFloorShells();
            if (shells == null || shells.Count == 0) return;

            // Rematerializar si el set de shells cambió respecto del materializado —
            // pasa tras una transición de piso, que regenera el grafo con GUIDs nuevos
            // (#158). Sin esto el floor view mostraría el layout del piso anterior.
            if (_shellGOs.Count > 0)
            {
                if (ShellSetMatches(shells)) return;
                ClearShellGOs();
            }

            if (_shellRoot == null)
            {
                var rootGO = new GameObject("FloorShells");
                _shellRoot = rootGO.transform;
            }

            EnsureShellMaterial();

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
                if (collider != null) DestroyObject(collider);

                cube.SetActive(false);
                _shellGOs[id] = cube;

                if (shell.Icon != null)
                {
                    var icon = CreateShellIcon(id, shell);
                    icon.SetActive(false);
                    _shellIcons[id] = icon;
                }
            }
        }

        /// <summary>
        /// Crea el <see cref="SpriteRenderer"/> del ícono de sala flotando sobre la cara
        /// superior del shell, orientado hacia la cámara del floor view (ángulo fijo ⇒ se
        /// billboardea una sola vez al materializar). Se parenta al root <b>sin escalar</b>
        /// —no al cubo— para no heredar la escala no-uniforme del shell (x/z = tamaño de
        /// sala, y ≈ 1) que deformaría el sprite.
        /// </summary>
        private GameObject CreateShellIcon(Guid id, FloorShell shell)
        {
            var iconGO = new GameObject($"ShellIcon_{id:N}");
            iconGO.transform.SetParent(_shellRoot, worldPositionStays: false);
            iconGO.transform.position = shell.WorldPosition + Vector3.up * (shell.Size.y * 0.5f + IconHeightOffset);
            iconGO.transform.localScale = Vector3.one * IconWorldSize;

            var cam = Camera.main;
            iconGO.transform.rotation = cam != null
                ? cam.transform.rotation
                : Quaternion.Euler(90f, 0f, 0f); // fallback: plano apoyado sobre la cara superior.

            var sr = iconGO.AddComponent<SpriteRenderer>();
            sr.sprite = shell.Icon;
            return iconGO;
        }

        /// <summary>
        /// <c>true</c> si los shell GameObjects materializados corresponden exactamente
        /// al set de shells actual del dungeon (mismas keys). Como los GUIDs de room
        /// instance se regeneran por piso, un piso nuevo nunca matchea ⇒ fuerza rebuild.
        /// </summary>
        private bool ShellSetMatches(IReadOnlyDictionary<Guid, FloorShell> shells)
        {
            if (_shellGOs.Count != shells.Count) return false;
            foreach (var id in shells.Keys)
                if (!_shellGOs.ContainsKey(id)) return false;
            return true;
        }

        private void ClearShellGOs()
        {
            foreach (var go in _shellGOs.Values)
                if (go != null) DestroyObject(go);
            _shellGOs.Clear();

            foreach (var icon in _shellIcons.Values)
                if (icon != null) DestroyObject(icon);
            _shellIcons.Clear();
        }

        /// <summary>
        /// Destruye un objeto respetando el modo del editor: <c>Destroy</c> en runtime,
        /// <c>DestroyImmediate</c> en edit mode (evita el error "Destroy may not be called
        /// from edit mode" y hace al controller testeable en EditMode).
        /// </summary>
        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        private void EnsureShellMaterial()
        {
            if (_sharedShellMaterial != null) return;

            if (_config != null && _config.ShellMaterial != null)
            {
                _sharedShellMaterial = _config.ShellMaterial;
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogWarning(LogPrefix + "URP Unlit shader not found, falling back to Standard.");
                shader = Shader.Find("Standard");
            }
            _sharedShellMaterial = new Material(shader);
            _sharedShellMaterial.color = _config != null ? _config.ShellColor : new Color(0.1f, 0.1f, 0.15f, 0.85f);
        }
    }
}
