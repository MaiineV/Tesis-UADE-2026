using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Selection
{
    public class SelectionRequest
    {
        /// <summary>
        /// Preview de trayectoria por hover, para selecciones por direccion (Items
        /// Activos §A4: Justa de Justicia, Grapple Claw). Devuelve las casillas a pintar
        /// para el proxy hovered, o null/vacio para no pintar nada extra. Null = sin
        /// preview de trayectoria (comportamiento intacto: combate y el resto de
        /// selecciones no lo setean).
        /// </summary>
        public Func<GridCoord, IReadOnlyList<GridCoord>> HoverPreview;

        /// <summary>Estilo del preview de <see cref="HoverPreview"/>. Default "path".</summary>
        public string HoverPreviewStyle = "path";

        public SelectionSettings Settings;
        public List<TargetRef> ValidTargets;
        public Guid OwnerGuid;
        public string HighlightStyle;

        /// <summary>
        /// Casillas del rango geométrico completo de un ataque, pintadas como underlay con
        /// <see cref="RangeHighlightStyle"/> DEBAJO de <see cref="ValidTargets"/>. Son solo
        /// visuales: NO se agregan a las casillas clickeables, así que un slot del rango sin
        /// target sigue sin hacer nada. Null/empty = sin underlay (movimiento, heal, puertas
        /// no lo setean → comportamiento intacto).
        /// </summary>
        public IReadOnlyCollection<GridCoord> RangeTiles;

        /// <summary>
        /// Estilo del underlay de rango (default "range" en el controller). Solo aplica si
        /// <see cref="RangeTiles"/> tiene casillas.
        /// </summary>
        public string RangeHighlightStyle;

        /// <summary>
        /// Casillas "frente a puerta" (Exploración). Se pintan con el estilo "door" y se
        /// tratan como targets válidos extra; al seleccionarlas el flujo de Exploración
        /// cruza a la sala vecina en vez de mover. Null/empty = sin puertas (combate y
        /// el resto de selecciones no lo setean → comportamiento intacto).
        /// </summary>
        public HashSet<GridCoord> DoorTiles;

        /// <summary>
        /// Si es true, NO se pinta el rango de casillas válidas (el tinte de fondo). Solo
        /// se muestra el preview del path (verde) al hacer hover y las puertas. Usado por
        /// el movimiento de Exploración, que está siempre armado: pintar todo el rango en
        /// permanencia es molesto. Default false → combate y demás selecciones intactas.
        /// </summary>
        public bool SuppressRangeHighlight;

        /// <summary>
        /// Si es true, NO se pinta NINGÚN tinte de piso durante la selección: ni el preview
        /// del path (verde) en hover, ni el tinte "selected" de la casilla clickeada. Las
        /// puertas se siguen pintando (viven aparte). Usado por el movimiento de Exploración
        /// (siempre armado): con permanencia cualquier tinte que siga al cursor molesta, el
        /// user quiere click-to-move limpio. Default false → combate y demás intactos.
        /// </summary>
        public bool SuppressPathPreview;
    }
}
