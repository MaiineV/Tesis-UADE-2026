using System;
using System.Collections.Generic;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Fuente de estados para la fila de <see cref="PlayerStatusIconsView"/>. Hoy la única
    /// implementación es <see cref="ClassPassiveStatusProvider"/>; el sistema de efectos de
    /// estado que viene entra sumando otro provider, sin tocar la vista.
    /// </summary>
    public interface IStatusIconProvider
    {
        /// <summary>
        /// Agrega a <paramref name="into"/> los estados de este provider para el owner dado,
        /// en el orden en que deben mostrarse. Sin estados que mostrar, no agrega nada.
        /// </summary>
        /// <remarks>
        /// Escribe en una lista prestada en vez de devolver una nueva: esto corre en cada
        /// refresh del HUD (cambios de HP, de modifiers, de turno) y no vale la pena generar
        /// basura por frame para dos íconos.
        /// </remarks>
        void Collect(Guid ownerGuid, List<StatusIconState> into);
    }
}
