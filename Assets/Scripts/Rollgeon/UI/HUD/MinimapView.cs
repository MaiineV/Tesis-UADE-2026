using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dungeon;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Minimapa estilo Isaac: una Image por sala descubierta (actual / visitada /
    /// adyacente sin visitar — fog of war de <see cref="RoomDiscovery"/> vía
    /// <see cref="MinimapModel"/>), sprite por estado+tipo (<see cref="MinimapSpriteMap"/>).
    /// Las celdas viven en posiciones fijas dentro del contenedor y la rotación con la
    /// cámara gira el CONTENEDOR entero (<see cref="MinimapLayout.ContainerAngle"/>) —
    /// el mapa rota rígido, como una brújula.
    /// </summary>
    /// <remarks>
    /// Dos instancias conviven (ExplorationHUD siempre visible; CombatHUD detrás del
    /// toggle Tab de <see cref="CombatRightPanelSwitcher"/>) — las screens son
    /// mutuamente exclusivas, así que la doble suscripción a OnRoomEntered es inocua.
    /// El yaw se pollea del rig real (<c>Camera.main</c>) y no de CameraFacing: el enum
    /// no incluye el DiagonalYawOffset y el evento dispara al inicio del tween.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Minimap View")]
    public class MinimapView : MonoBehaviour
    {
        [Title("Minimap — Widget refs")]
        [Tooltip("Settings compartidos (sprites + layout). Los cablea el installer.")]
        [SerializeField]
        private MinimapSettingsSO _settings;

        [Tooltip("Padre de las celdas, centrado en el rect (la sala actual queda acá). " +
                 "El root debería tener RectMask2D para clipear los bordes.")]
        [SerializeField]
        private RectTransform _cellRoot;

        [ShowInInspector, ReadOnly]
        private bool _bound;

        private readonly List<Image> _cellPool = new List<Image>();
        private List<MinimapCell> _cells = new List<MinimapCell>();
        private Camera _camera;
        private float _appliedYaw = float.NaN;

        public bool IsBound => _bound;

        /// <summary>Celdas del último rebuild — hook de tests/debug.</summary>
        public IReadOnlyList<MinimapCell> CurrentCells => _cells;

        public void Bind(Guid playerGuid)
        {
            if (_bound) Unbind();

            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            _bound = true;
            Rebuild();
        }

        public void Unbind()
        {
            if (!_bound) return;
            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEnteredHandler);
            _bound = false;
        }

        private void OnDisable()
        {
            // La screen que nos hostea se apaga al cambiar de HUD — mismo criterio que
            // TurnQueueView: soltar las subs; el próximo BindAll del host re-bindea.
            Unbind();
        }

        private void OnRoomEnteredHandler(params object[] args)
        {
            // Cubre también cambio de piso: los GUIDs se regeneran y OnRoomEntered
            // dispara al pisar la primera sala del piso nuevo — rebuild total, sin
            // cachear ids entre rebuilds.
            Rebuild();
        }

        /// <summary>
        /// Reconstruye el modelo desde <see cref="IDungeonService"/> y re-aplica
        /// sprites + posiciones. Público para tests/installer.
        /// </summary>
        public void Rebuild()
        {
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                && dungeon?.CurrentRoomInstance != null)
            {
                _cells = MinimapModel.Build(
                    dungeon.GetAllRoomInstances(), dungeon.CurrentRoomInstance.InstanceId);
            }
            else
            {
                _cells = new List<MinimapCell>();
            }

            ApplyCells();
            _appliedYaw = float.NaN; // fuerza re-aplicar la rotación con el yaw actual.
            ApplyRotation(ResolveYaw());
        }

        private void Update()
        {
            if (!_bound || _cells.Count == 0) return;

            float yaw = ResolveYaw();
            if (!float.IsNaN(_appliedYaw) && Mathf.Approximately(yaw, _appliedYaw)) return;
            ApplyRotation(yaw);
        }

        // Sprite + activación del pool + posición local FIJA de cada celda. La rotación
        // no toca las celdas: gira el contenedor entero en ApplyRotation.
        private void ApplyCells()
        {
            // Fallback a Transform pelado: rigs de test viejos cuelgan la view de un GO
            // sin RectTransform — las celdas (que sí tienen el suyo) se parentan igual.
            Transform root = _cellRoot != null ? _cellRoot : transform;

            while (_cellPool.Count < _cells.Count)
            {
                var go = new GameObject($"Cell_{_cellPool.Count}", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(root, worldPositionStays: false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                _cellPool.Add(image);
            }

            float cellSize = _settings != null ? _settings.CellSize : 32f;
            float pitch = _settings != null ? _settings.Pitch : 35f;
            for (int i = 0; i < _cellPool.Count; i++)
            {
                bool active = i < _cells.Count;
                _cellPool[i].gameObject.SetActive(active);
                if (!active) continue;

                _cellPool[i].rectTransform.sizeDelta = new Vector2(cellSize, cellSize);
                _cellPool[i].rectTransform.anchoredPosition =
                    MinimapLayout.CellPosition(_cells[i].Offset, pitch);
                _cellPool[i].sprite = _settings != null
                    ? _settings.CellSprite(MinimapSpriteMap.Resolve(_cells[i]))
                    : null;
                _cellPool[i].enabled = _cellPool[i].sprite != null;
            }
        }

        // El mapa entero gira rígido: una sola rotación Z en el contenedor de celdas.
        private void ApplyRotation(float yaw)
        {
            float extra = _settings != null ? _settings.ExtraYawDegrees : 0f;
            bool clockwise = _settings == null || _settings.Clockwise;

            var root = _cellRoot != null ? _cellRoot.transform : transform;
            root.localRotation = Quaternion.Euler(
                0f, 0f, MinimapLayout.ContainerAngle(yaw, extra, clockwise));
            _appliedYaw = yaw;
        }

        // Yaw del rig real, con re-resolve lazy (Camera.main es null en EditMode/tests
        // y durante loads — el minimapa simplemente queda sin rotar hasta que aparezca).
        private float ResolveYaw()
        {
            if (_camera == null) _camera = Camera.main;
            return _camera != null ? _camera.transform.eulerAngles.y : 0f;
        }
    }
}
