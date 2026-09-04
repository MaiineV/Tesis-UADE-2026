using System;
using Rollgeon.Grid;

namespace Rollgeon.Items.Active.Targeting
{
    /// <summary>
    /// Un efecto de banda que restringe cuales de los targets validos de la seleccion
    /// realmente sirven (Bottle'o Thunder: LoS + inmunidad a stun). El servicio
    /// intersecta los tiles validos de la seleccion con cada efecto de banda que
    /// implemente esta interface antes de abrir la seleccion.
    /// </summary>
    public interface IActiveItemTargetFilter
    {
        bool IsValidTarget(Guid owner, GridCoord coord);
    }
}
