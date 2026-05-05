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
    }
}
