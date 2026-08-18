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
    /// <para>
    /// Lo que el jugador tiene que resolver de un vistazo no es <i>de quién</i> es la amenaza sino
    /// <i>cuándo</i> cobra. Hasta ahora "se marcó" y "detona ahora" entraban por la misma puerta
    /// (<c>Show</c> + un color), así que la única diferencia posible entre ambas era el matiz — y el
    /// matiz ya estaba ocupado distinguiendo fuentes (fuego vs. hielo vs. canal auxiliar). El estado
    /// viaja aparte del tint justamente para que el patrón quede libre para el <i>cuándo</i>.
    /// </para>
    /// <para>
    /// Se serializa por índice (styles de bootstrap, campos de assets de jefe): <b>los valores nuevos
    /// van al final</b>. Reordenar reasigna el estado de todo lo ya autorado.
    /// </para>
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
        /// <param name="tint">Null ⇒ el color por defecto del estado. Pasarlo explícito es lo que
        /// permite que dos fuentes distintas compartan estado sin confundirse entre ellas.</param>
        void Show(Guid sourceGuid, IEnumerable<GridCoord> tiles, ThreatOverlayState state, Color? tint = null);

        /// <summary>Apaga el overlay de <paramref name="sourceGuid"/> (telegraph resuelto/cancelado).</summary>
        void Clear(Guid sourceGuid);

        /// <summary>Apaga todos los overlays (fin de combate / fin de run).</summary>
        void ClearAll();
    }
}
