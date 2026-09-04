using System;
using System.Collections.Generic;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Estado Sangrado (Feature#0084, Blood Transfusion): daño por turno acumulable —
    /// a diferencia de <see cref="IPoisonService"/> (que REFRESCA), cada aplicación
    /// AGREGA un stack nuevo con su propia duración. Tickea al INICIO del turno del
    /// sangrante, un solo golpe de pipeline por turno con el daño de TODOS los stacks
    /// vivos sumado.
    /// </summary>
    public interface IBleedService
    {
        /// <summary>
        /// Agrega <paramref name="stacks"/> stacks nuevos (cada uno con su propia
        /// duración de <see cref="BleedService.TurnsPerStack"/> turnos) a <paramref name="entity"/>.
        /// <paramref name="source"/> viaja como SourceId de ESE stack — el kill credit de
        /// cada stack es independiente. No-op si el guid es <see cref="Guid.Empty"/> o
        /// <paramref name="stacks"/> &lt;= 0.
        /// </summary>
        void AddStack(Guid entity, Guid source, int stacks = 1);

        /// <summary><c>true</c> si a <paramref name="entity"/> le queda al menos 1 stack vivo.</summary>
        bool IsBleeding(Guid entity);

        /// <summary>Cantidad de stacks vivos (0 = no sangrando).</summary>
        int GetStacks(Guid entity);

        /// <summary>
        /// Turnos restantes del stack MÁS DURADERO (el que más tarde en vencer). Es lo que
        /// muestra el badge de la HUD — "Sangrado ×N" con N = <see cref="GetStacks"/>, la
        /// duración visible es la del stack que sobrevive más tiempo.
        /// </summary>
        int GetMaxRemainingTurns(Guid entity);

        /// <summary>Cura todo el Sangrado de una entidad (dispara <c>OnBleedExpired</c> si tenía stacks).</summary>
        void Clear(Guid entity);

        /// <summary>Teardown de scope: limpia todo SIN eventos.</summary>
        void ClearAll();
    }
}
