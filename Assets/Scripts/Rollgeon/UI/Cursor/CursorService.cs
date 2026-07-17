using System.Collections.Generic;
using Rollgeon.Entities.Visuals;
using Rollgeon.Grid;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Cerebro del cursor custom: cada frame decide el estado (default / hover /
    /// click-vacío / click-hover) según si hay algo interactivo bajo el mouse, y
    /// mueve la <see cref="CursorView"/>. Oculta el cursor del sistema.
    /// </summary>
    /// <remarks>
    /// "Hovereable" = UI interactiva (un <see cref="Selectable"/> habilitado o un
    /// <see cref="ICursorHoverable"/>) o un objeto de mundo con
    /// <see cref="EntityPawn"/> / <see cref="ICursorHoverable"/>. Los scrims de
    /// fondo (pausa, opciones) NO cuentan aunque tapen el raycast, porque no son
    /// Selectables — por eso no se usa <c>IsPointerOverGameObject</c>.
    /// </remarks>
    public sealed class CursorService : MonoBehaviour, ICursorService
    {
        private CursorView _view;
        private CursorSettingsSO _settings;
        private bool _visible = true;

        private Camera _worldCamera;
        private PointerEventData _pointerData;
        private readonly List<RaycastResult> _uiHits = new();

        public void Configure(CursorView view, CursorSettingsSO settings)
        {
            _view = view;
            _settings = settings;
        }

        private void OnEnable() => ApplySystemCursor();

        private void OnDisable()
        {
            // Al apagarse (ej. salir de play), devolver el cursor del sistema.
            UnityEngine.Cursor.visible = true;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplySystemCursor();
            if (_view != null) _view.gameObject.SetActive(visible);
        }

        private void ApplySystemCursor()
        {
            // El cursor custom reemplaza al del sistema; si lo ocultamos, vuelve el nativo.
            UnityEngine.Cursor.visible = !_visible;
        }

        private void Update()
        {
            if (!_visible || _view == null || _settings == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // El SO resetea Cursor.visible al perder/recuperar foco — re-asegurar.
            if (UnityEngine.Cursor.visible) UnityEngine.Cursor.visible = false;

            Vector2 pos = mouse.position.ReadValue();
            _view.SetPosition(pos);

            bool pressed = mouse.leftButton.isPressed;
            bool hoverable = IsUiHoverable(pos) || IsWorldHoverable(pos);
            _view.SetState(CursorStateResolver.Resolve(pressed, hoverable));
        }

        private bool IsUiHoverable(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;

            _pointerData ??= new PointerEventData(es);
            _pointerData.position = screenPos;
            _uiHits.Clear();
            es.RaycastAll(_pointerData, _uiHits);

            foreach (var hit in _uiHits)
            {
                var go = hit.gameObject;
                if (go == null) continue;

                // Un Selectable habilitado (botón, toggle...) o un marcador explícito.
                // Los fondos/scrims no son Selectables → no dan hover.
                var selectable = go.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.IsInteractable()) return true;
                if (go.GetComponentInParent<ICursorHoverable>() != null) return true;
            }

            return false;
        }

        private bool IsWorldHoverable(Vector2 screenPos)
        {
            var cam = ResolveWorldCamera();
            if (cam == null) return false;

            // Pipeline pixel-art: la pantalla no coincide con el RT de la cámara.
            Vector2 rt = RenderTextureCursor.ScreenToRt(
                screenPos, Screen.width, Screen.height, cam.pixelWidth, cam.pixelHeight);
            var ray = cam.ScreenPointToRay(rt);

            if (!Physics.Raycast(ray, out var hit, _settings.WorldRaycastDistance)) return false;

            // Enemigos/héroe (EntityPawn) e interactuables (ICursorHoverable). Los
            // tiles del piso no matchean ninguno → apuntar al piso es "nada".
            return hit.collider.GetComponentInParent<EntityPawn>() != null
                   || hit.collider.GetComponentInParent<ICursorHoverable>() != null;
        }

        private Camera ResolveWorldCamera()
        {
            if (_worldCamera != null && _worldCamera.isActiveAndEnabled) return _worldCamera;
            _worldCamera = Camera.main;
            return _worldCamera;
        }
    }
}
