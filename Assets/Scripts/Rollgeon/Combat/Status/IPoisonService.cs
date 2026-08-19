using System;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Estado Envenenado (GDD Casillas Especiales — Veneno): daño por turno durante N
    /// turnos, tickeado al INICIO del turno del envenenado. Sin stacking: re-aplicar solo
    /// refresca la duración, nunca suma daño ni turnos.
    /// </summary>
    public interface IPoisonService
    {
        /// <summary>
        /// Aplica (o refresca) el veneno. <paramref name="sourceId"/> es quien lo causó —
        /// la casilla, típicamente — y viaja como SourceId del daño de cada tick, así el
        /// kill credit funciona aunque la casilla ya haya expirado.
        /// </summary>
        void ApplyPoison(Guid entity, int damagePerTurn, int turns, Guid sourceId);

        bool IsPoisoned(Guid entity);

        /// <summary>Turnos de veneno restantes; 0 = no envenenado.</summary>
        int GetPoisonTurns(Guid entity);

        /// <summary>Cura el veneno de una entidad (dispara <c>OnPoisonExpired</c>).</summary>
        void Clear(Guid entity);

        /// <summary>Teardown de scope: limpia todo SIN eventos.</summary>
        void ClearAll();
    }
}
