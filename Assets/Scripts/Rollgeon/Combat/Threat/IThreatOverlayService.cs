using System;
using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Qué le está por pasar a una casilla amenazada.
    /// </summary>
    /// <remarks>
    /// Se serializa por índice (styles de bootstrap, campos de assets de jefe): <b>los valores nuevos
    /// van al final</b>. Reordenar reasigna el estado de todo lo ya autorado.
    /// </remarks>
    public enum ThreatOverlayState
    {
        /// <summary>Rayado. Se marcó: detona el turno que viene. El ciclo mark → execute de siempre.</summary>
        Marked = 0,

        /// <summary>Sólido. Detona <b>ahora</b>: quien esté adentro ya cobró o está por cobrar.</summary>
        Detonating = 1,

        /// <summary>Punteado. Cae en dos turnos — aviso temprano, todavía sobra margen para caminar.</summary>
        Incoming = 2,

        /// <summary>Damero. Zona segura declarada (telegraph invertido: el daño es todo lo de afuera).</summary>
        Safe = 3,
    }

    /// <summary>
    /// Canal visual del telegraph de los Bosses: quads/sprites pooled flotando
    /// sobre las casillas amenazadas. Es independiente del tinte de piso de
    /// <see cref="ITileHighlightService"/>, así que convive con el highlight de
    /// move/path del jugador (que pinta y limpia sus tiles a su antojo) sin que
    /// ninguno pise al otro.
    /// </summary>
    /// <remarks>
    /// Es por fuente y un <c>Show</c> reemplaza al anterior de esa misma fuente: dos avisos
    /// simultáneos del mismo jefe tienen que entrar por dos fuentes derivadas distintas o el segundo
    /// apaga el dibujo del primero. Sólo lo saca un <see cref="Clear"/> o un <c>Show</c> de su
    /// fuente, o el fin de combate/run — nada por turno ni por ronda.
    /// </remarks>
    public interface IThreatOverlayService
    {
        /// <summary>Muestra (o reemplaza) el área amenazada de <paramref name="sourceGuid"/> con el
        /// naranja de advertencia por defecto, en <see cref="ThreatOverlayState.Marked"/>.</summary>
        void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles);

        /// <summary>
        /// Igual que <see cref="Show(Guid,IEnumerable{GridCoord})"/> pero con color propio, para que
        /// dos amenazas simultáneas (fuego vs. hielo) no se lean idénticas. También
        /// <see cref="ThreatOverlayState.Marked"/>.
        /// </summary>
        /// <remarks>El alpha del tint lo sobrescribe el pulso del overlay en runtime; lo que aporta
        /// el parámetro es el matiz.</remarks>
        void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, Color tint);

        /// <summary>
        /// Muestra (o reemplaza) el área de <paramref name="sourceGuid"/> declarando además en qué
        /// momento del aviso está: el <paramref name="state"/> elige patrón y banda de pulso, el
        /// <paramref name="tint"/> sigue siendo la identidad de la fuente.
        /// </summary>
        /// <param name="tint">Null ⇒ el color por defecto del estado.</param>
        void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state, Color? tint = null);

        /// <summary>Apaga el overlay de <paramref name="sourceGuid"/> (telegraph resuelto/cancelado).</summary>
        void Clear(Guid sourceGuid);

        /// <summary>Apaga todos los overlays (fin de combate / fin de run).</summary>
        void ClearAll();
    }
}
