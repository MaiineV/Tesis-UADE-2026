using System;

namespace Rollgeon.Effects.Selection
{
    public interface ISelectionController
    {
        void BeginSelection(SelectionRequest request);
        void OnTargetClicked(TargetRef target);

        /// <summary>
        /// Notifica al controller que el cursor pasó por <paramref name="target"/> (o
        /// <c>null</c> si salió de cualquier tile). Usado para previewar el camino A*
        /// hacia el destino hovered en selecciones de movimiento. No-op en otras selecciones.
        /// </summary>
        void OnTargetHovered(TargetRef target);

        void CancelSelection();
        event Action<TargetSelectionResult> OnSelectionCompleted;
        bool IsSelecting { get; }

        /// <summary>
        /// True si un preview transitorio (ej. hover de rango desde el HUD) puede pintar
        /// tiles sin pisar una selección activa visible. En Exploración el movimiento
        /// está siempre armado pero con rango suprimido, así que el hover sí puede pintar.
        /// </summary>
        bool CanOverlayHoverPreview { get; }

        /// <summary>
        /// Repinta el estado visual de la selección activa (rango + puertas) tras un
        /// <c>ClearAll</c> ajeno (ej. al salir el hover de rango del HUD). No-op sin
        /// selección activa.
        /// </summary>
        void RefreshHighlights();
    }
}
