using System;
using Rollgeon.Grid;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// Progreso de la caminata VISUAL de los pawns, por guid. Lo consume el spawn diferido de
    /// las casillas de rastro (Incendiario / Rastro tóxico / Sendero de espinas): la lógica
    /// coloca la casilla al instante — el grid ya movió al dueño —, pero el arte espera a que
    /// su cuerpo haya cruzado la celda. Run-scope, lo registra <c>EntityVisualServiceBootstrap</c>;
    /// sin él (tests, escenas sin visuales) todo aparece al instante.
    /// </summary>
    public interface IPawnWalkTracker
    {
        /// <summary>
        /// <c>true</c> si el pawn de <paramref name="entity"/> está caminando y todavía no
        /// abandonó <paramref name="coord"/> (es la celda de la que sale o una por venir).
        /// </summary>
        bool IsWalkingThrough(Guid entity, GridCoord coord);

        /// <summary>El pawn abandonó una celda: llegó a la siguiente. Args: (entity, celda abandonada).</summary>
        event Action<Guid, GridCoord> OnCellLeft;

        /// <summary>La caminata del pawn terminó: llegó, abortó el reroute o la cortó un stop/snap.</summary>
        event Action<Guid> OnWalkEnded;
    }
}
