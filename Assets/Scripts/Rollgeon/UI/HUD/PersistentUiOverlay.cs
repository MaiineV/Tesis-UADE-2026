using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Canvas ScreenSpace-Overlay persistente (<c>DontDestroyOnLoad</c>) creado on-demand.
    /// Hostea los números flotantes para que <b>sobrevivan al teardown del CombatHUD</b>:
    /// así el combate puede terminar instantáneamente (sin la pausa de "animación de
    /// muerte") y los números igual terminan de animar.
    /// </summary>
    /// <remarks>
    /// Mismo patrón que <see cref="Rollgeon.Patterns.CoroutineHost"/>. Copia el
    /// <see cref="CanvasScaler"/> del canvas de referencia (el del HUD de combate) la
    /// primera vez que se crea, para que los números se vean del mismo tamaño/posición
    /// que cuando vivían dentro del HUD.
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    public sealed class PersistentUiOverlay : MonoBehaviour
    {
        private static PersistentUiOverlay _instance;
        private RectTransform _container;

        /// <summary>
        /// RectTransform full-stretch donde parentear los números. Crea el canvas la
        /// primera vez. <paramref name="reference"/> = scaler a copiar (opcional, solo
        /// se usa en la creación inicial).
        /// </summary>
        public static RectTransform GetContainer(CanvasScaler reference = null)
        {
            if (_instance == null) Create(reference);
            return _instance != null ? _instance._container : null;
        }

        private static void Create(CanvasScaler reference)
        {
            var go = new GameObject("[PersistentUiOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PersistentUiOverlay>();

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Por encima del HUD para que los números no queden tapados, pero sin pisar
            // overlays críticos (fades de transición suelen usar el tope absoluto).
            canvas.sortingOrder = 30000;

            var scaler = go.AddComponent<CanvasScaler>();
            if (reference != null)
            {
                scaler.uiScaleMode = reference.uiScaleMode;
                scaler.referenceResolution = reference.referenceResolution;
                scaler.screenMatchMode = reference.screenMatchMode;
                scaler.matchWidthOrHeight = reference.matchWidthOrHeight;
                scaler.scaleFactor = reference.scaleFactor;
                scaler.referencePixelsPerUnit = reference.referencePixelsPerUnit;
            }

            // Container hijo con RectTransform explícito (full-stretch) — garantiza que
            // los números, que setean su position en screen-space, tengan un padre UI
            // válido sin depender de que el root del Canvas tenga RectTransform.
            var containerGO = new GameObject("FloatingNumbers", typeof(RectTransform));
            var rt = (RectTransform)containerGO.transform;
            rt.SetParent(go.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _instance._container = rt;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
