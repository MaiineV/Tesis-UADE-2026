using Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Levanta el cursor custom apenas carga la primera escena y lo mantiene
    /// vivo (DontDestroyOnLoad) para todas las escenas. Se auto-dispara con
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> — no necesita
    /// wiring de escena ni entrada en <c>ServiceBootstrap</c> (esa lista es
    /// Odin y el cursor es global always-on).
    /// </summary>
    public static class CursorBootstrap
    {
        private const int OverlaySortingOrder = 32760; // sobre loading (31000) y todo lo demás.
        private static CursorService _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var settings = Resources.Load<CursorSettingsSO>("Cursor/CursorSettings");
            if (settings == null)
            {
                Debug.LogWarning("[CursorBootstrap] No se encontró Resources/Cursor/CursorSettings — " +
                                 "corré 'Rollgeon → Cursor → Setup'. Cursor custom desactivado.");
                return;
            }

            var root = new GameObject("[Cursor]");
            Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = OverlaySortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // Sin GraphicRaycaster a propósito: el cursor no debe bloquear input.

            var imageGo = new GameObject("CursorImage", typeof(RectTransform), typeof(Image));
            var imageRect = (RectTransform)imageGo.transform;
            imageRect.SetParent(root.transform, worldPositionStays: false);
            var image = imageGo.GetComponent<Image>();
            image.raycastTarget = false;

            var view = root.AddComponent<CursorView>();
            view.Configure(imageRect, image, settings);

            _instance = root.AddComponent<CursorService>();
            _instance.Configure(view, settings);
            _instance.SetVisible(true);

            ServiceLocator.AddService<ICursorService>(_instance, ServiceScope.Global);
        }
    }
}
