using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Items.Active.Targeting
{
    /// <summary>
    /// Un efecto de banda que se dirige por <see cref="Cardinal"/> en vez de por tile
    /// (Justa de Justicia, Grapple Claw). Cuando algun efecto de la banda que corre lo
    /// implementa, <c>ActiveItemActivationService</c> arma el flujo de direccion del
    /// GDD §A4 en vez de la seleccion de tile normal.
    /// </summary>
    public interface IDirectionTargetedEffect
    {
        /// <summary>
        /// Casillas que recorreria la trayectoria desde <paramref name="origin"/> hacia
        /// <paramref name="dir"/>. Vacio = direccion invalida (sin nada que recorrer) —
        /// el servicio la descarta como proxy de seleccion. La cara del dado decide la
        /// distancia real recien al resolver; esto es solo el preview.
        /// </summary>
        IReadOnlyList<GridCoord> PreviewTrajectory(Guid owner, GridCoord origin, Cardinal dir);
    }
}
