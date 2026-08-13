using System;
using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

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
        /// <summary>Muestra (o reemplaza) el área amenazada de <paramref name="sourceGuid"/> con el
        /// naranja de advertencia por defecto.</summary>
        void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles);

        /// <summary>
        /// Igual que <see cref="Show(Guid,IEnumerable{GridCoord})"/> pero con color propio, para que
        /// dos amenazas simultáneas (fuego vs. hielo) no se lean idénticas.
        /// </summary>
        /// <remarks>El alpha del tint lo sobrescribe el pulso del overlay en runtime; lo que aporta
        /// el parámetro es el matiz.</remarks>
        void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint);

        /// <summary>Apaga el overlay de <paramref name="sourceGuid"/> (telegraph resuelto/cancelado).</summary>
        void Clear(Guid sourceGuid);

        /// <summary>Apaga todos los overlays (fin de combate / fin de run).</summary>
        void ClearAll();
    }
}
