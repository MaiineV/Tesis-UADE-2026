using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Vista del cursor custom: una <see cref="Image"/> en un canvas overlay que
    /// sigue al mouse y cambia de sprite según el estado. Sin lógica de decisión
    /// (eso vive en <see cref="CursorService"/>).
    /// </summary>
    public sealed class CursorView : MonoBehaviour
    {
        private RectTransform _rect;
        private Image _image;
        private CursorSettingsSO _settings;
        private CursorState _current = (CursorState)(-1);

        public void Configure(RectTransform rect, Image image, CursorSettingsSO settings)
        {
            _rect = rect;
            _image = image;
            _settings = settings;
            _image.raycastTarget = false;
            _current = (CursorState)(-1);
        }

        public void SetPosition(Vector2 screenPos)
        {
            // Canvas ScreenSpaceOverlay: la posición del rect ES la de pantalla.
            if (_rect != null) _rect.position = screenPos;
        }

        public void SetState(CursorState state)
        {
            if (state == _current || _settings == null || _image == null) return;
            _current = state;

            var sprite = _settings.SpriteFor(state);
            _image.sprite = sprite;
            _image.enabled = sprite != null;
            if (sprite == null) return;

            _rect.pivot = _settings.HotspotPivot;
            _rect.sizeDelta = sprite.rect.size * _settings.Scale;
        }
    }
}
