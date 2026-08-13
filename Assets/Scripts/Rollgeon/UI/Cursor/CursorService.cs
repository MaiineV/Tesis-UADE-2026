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
    /// click-vacío / click-hover) según si hay algo interactivo bajo el mouse y
    /// aplica la textura vía <c>Cursor.SetCursor</c> (cursor de hardware: lo
    /// compone el OS, sin latencia ni doble puntero — el software con canvas
    /// overlay iba un frame atrás del cursor del sistema).
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
        private CursorSettingsSO _settings;
        private bool _visible = true;
        private CursorState _current = (CursorState)(-1);

        private Camera _worldCamera;
        private PointerEventData _pointerData;
        private readonly List<RaycastResult> _uiHits = new();

        public void Configure(CursorSettingsSO settings)
        {
            _settings = settings;
            _current = (CursorState)(-1);
        }

        private void OnDisable()
        {
            // Al apagarse (ej. salir de play), devolver el cursor del sistema.
            RestoreSystemCursor();
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible)
            {
                // Re-aplicar el estado actual en el próximo Update.
                _current = (CursorState)(-1);
            }
            else
            {
                RestoreSystemCursor();
            }
        }

        private void Update()
        {
            if (!_visible || _settings == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.isPressed;
            bool hoverable = IsUiHoverable(pos) || IsWorldHoverable(pos);

            var state = CursorStateResolver.Resolve(pressed, hoverable);
            if (state != _current) Apply(state);
        }

        private void Apply(CursorState state)
        {
            _current = state;
            var texture = _settings.CursorFor(state);
            // Sin textura (setup no corrido) degrada a la flecha del sistema.
            UnityEngine.Cursor.SetCursor(texture, HotspotPixels(texture), CursorMode.Auto);
        }

        // SetCursor mide el hotspot en píxeles desde arriba-izquierda; el pivot
        // del settings es normalizado con origen abajo-izquierda.
        private Vector2 HotspotPixels(Texture2D texture)
        {
            if (texture == null || _settings == null) return Vector2.zero;
            return new Vector2(
                _settings.HotspotPivot.x * texture.width,
                (1f - _settings.HotspotPivot.y) * texture.height);
        }

        private static void RestoreSystemCursor()
        {
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
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

            // Enemigos/héroe (EntityPawn implementa ICursorHoverable) e interactuables.
            // Los tiles del piso no matchean → apuntar al piso es "nada".
            //
            // Recorre TODOS los hits (PawnPicker), no el primero: con un solo hit, una
            // pared o un prop delante del enemigo apagaba el cursor de hover aunque el
            // click SÍ lo fuera a targetear. El cursor tiene que decir lo mismo que el click.
            return PawnPicker.TryPick<ICursorHoverable>(ray, out _, _settings.WorldRaycastDistance);
        }

        private Camera ResolveWorldCamera()
        {
            if (_worldCamera != null && _worldCamera.isActiveAndEnabled) return _worldCamera;
            _worldCamera = Camera.main;
            return _worldCamera;
        }
    }
}
