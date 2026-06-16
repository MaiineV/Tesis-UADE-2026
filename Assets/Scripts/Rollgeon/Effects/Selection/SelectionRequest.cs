using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Effects.Selection
{
    public class SelectionRequest
    {
        public SelectionSettings Settings;
        public List<TargetRef> ValidTargets;
        public Guid OwnerGuid;
        public string HighlightStyle;

        /// <summary>
        /// Casillas "frente a puerta" (Exploración). Se pintan con el estilo "door" y se
        /// tratan como targets válidos extra; al seleccionarlas el flujo de Exploración
        /// cruza a la sala vecina en vez de mover. Null/empty = sin puertas (combate y
        /// el resto de selecciones no lo setean → comportamiento intacto).
        /// </summary>
        public HashSet<GridCoord> DoorTiles;
    }
}
