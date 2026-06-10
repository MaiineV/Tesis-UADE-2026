using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Canal visual del telegraph de los Bosses: quads/sprites pooled flotando
    /// sobre las casillas amenazadas. Es independiente del tinte de piso de
    /// <see cref="ITileHighlightService"/>, así que convive con el highlight de
    /// move/path del jugador (que pinta y limpia sus tiles a su antojo) sin que
    /// ninguno pise al otro.
    /// </summary>
    public interface IThreatOverlayService
    {
        /// <summary>Muestra (o reemplaza) el área amenazada de <paramref name="sourceGuid"/>.</summary>
        void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles);

        /// <summary>Apaga el overlay de <paramref name="sourceGuid"/> (telegraph resuelto/cancelado).</summary>
        void Clear(Guid sourceGuid);

        /// <summary>Apaga todos los overlays (fin de combate / fin de run).</summary>
        void ClearAll();
    }
}
