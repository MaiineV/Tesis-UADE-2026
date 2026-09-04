using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Bridge de una sola lectura: el centro geométrico del área telegrafiada que
    /// <see cref="Rollgeon.Combat.AI.Decisions.AINode_ExecuteTelegraph"/> acaba de consumir, para
    /// que un VFX de impacto disparado en el MISMO windup (ej. <c>ArtilleryBombDrop</c>) sepa a
    /// qué celda volar sin tener que re-consultar <see cref="IThreatenedAreaService"/> — esa marca
    /// ya se sacó del servicio en el momento en que se consumió, así que un <c>TryPeek</c> tardío
    /// no encuentra nada.
    /// </summary>
    /// <remarks>
    /// <see cref="TryGet"/> NO consume la entrada — puede haber más de un lector por turno (el
    /// <c>FaceTarget</c> del nodo de ejecución Y, para el Artillery, el VFX de impacto que dispara
    /// el mismo windup). Es seguro porque el único escritor (<c>AINode_ExecuteTelegraph</c> /
    /// <c>AINode_ExecuteCharge</c>) siempre pisa la entrada con <see cref="Set"/> ANTES de que
    /// cualquiera la lea, cada vez que esa misma fuente cobra un telegraph nuevo — un lector tardío
    /// nunca puede ver un centro de un ciclo anterior sin que ya se haya sobrescrito.
    /// </remarks>
    public static class LastThreatenedAreaCenter
    {
        private static readonly Dictionary<Guid, GridCoord> _byOwner = new Dictionary<Guid, GridCoord>();

        /// <summary>Centro geométrico (promedio redondeado) de <paramref name="tiles"/> — para una
        /// forma simétrica (ej. el diamante de <c>DiamondAroundPlayer</c>) coincide exactamente con
        /// la celda de anclaje original.</summary>
        public static GridCoord ComputeCenter(IReadOnlyCollection<GridCoord> tiles)
        {
            if (tiles == null || tiles.Count == 0) return default;

            long sumX = 0, sumY = 0;
            foreach (var c in tiles) { sumX += c.X; sumY += c.Y; }

            int n = tiles.Count;
            return new GridCoord(
                (int)Math.Round(sumX / (double)n, MidpointRounding.AwayFromZero),
                (int)Math.Round(sumY / (double)n, MidpointRounding.AwayFromZero));
        }

        public static void Set(Guid ownerGuid, GridCoord center)
        {
            if (ownerGuid == Guid.Empty) return;
            _byOwner[ownerGuid] = center;
        }

        /// <summary>Lee sin consumir — <c>false</c> si no hay nada guardado para este owner.</summary>
        public static bool TryGet(Guid ownerGuid, out GridCoord center)
        {
            if (ownerGuid != Guid.Empty && _byOwner.TryGetValue(ownerGuid, out center))
                return true;
            center = default;
            return false;
        }

        /// <summary>Limpia todo — <c>OnCombatEnd</c>/<c>OnRunEnd</c>, mismo criterio que
        /// <see cref="IThreatenedAreaService.ClearAll"/>.</summary>
        public static void ClearAll() => _byOwner.Clear();
    }
}
