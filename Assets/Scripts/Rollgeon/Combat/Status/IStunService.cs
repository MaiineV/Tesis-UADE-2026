using System;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Stun por turnos: la entidad stuneada pierde su turno completo (no actúa, no tira dados).
    /// Primer consumidor previsto: el hazard de hielo del diseño de jefes — pisarlo stunea 1 turno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>API pura, sin acople al disparador.</b> Este servicio solo lleva la cuenta de turnos
    /// stuneados por entidad. Quién aplica el stun (hazard, ataque de boss, item) y quién lo
    /// consume (el skip de turno del <c>CombatController</c>) viven afuera. No se suscribe a
    /// ningún evento de hazards.
    /// </para>
    /// <para>
    /// <b>No hay stacking aditivo.</b> <see cref="ApplyStun"/> toma <c>max(actual, nuevo)</c>:
    /// el hielo se derrite al pisarlo, así que dos disparos seguidos no deben poder encadenar
    /// al jugador fuera del combate. Un stun de 1 turno sobre otro de 1 turno sigue siendo 1.
    /// </para>
    /// <para>
    /// <b>Ciclo de vida.</b> El estado es combat-scoped: <see cref="ClearAll"/> corre en
    /// <c>OnCombatEnd</c> y <c>OnRunEnd</c>. Ninguna entidad arrastra stun entre salas.
    /// </para>
    /// </remarks>
    public interface IStunService
    {
        /// <summary>
        /// Stunea a <paramref name="entity"/> por <paramref name="turns"/> turnos, tomando
        /// <c>max(restante, turns)</c>. No-op si el guid es <see cref="Guid.Empty"/> o si
        /// <paramref name="turns"/> &lt;= 0. Dispara <c>OnStunApplied</c>.
        /// </summary>
        void ApplyStun(Guid entity, int turns = 1);

        /// <summary><c>true</c> si a <paramref name="entity"/> le queda al menos 1 turno de stun.</summary>
        bool IsStunned(Guid entity);

        /// <summary>Turnos de stun restantes; <c>0</c> si no está stuneada.</summary>
        int GetStunTurns(Guid entity);

        /// <summary>
        /// Consume 1 turno de stun de <paramref name="entity"/>. Lo llama el skip de turno del
        /// combate: el turno que se está perdiendo es el que se descuenta acá. Dispara
        /// <c>OnStunExpired</c> cuando el contador llega a 0.
        /// </summary>
        /// <returns>
        /// <c>true</c> si había stun y se consumió un turno (el caller debe saltear el turno);
        /// <c>false</c> si la entidad no estaba stuneada (nada que saltear).
        /// </returns>
        bool ConsumeTurn(Guid entity);

        /// <summary>
        /// Cura el stun de una entidad puntual. Dispara <c>OnStunExpired</c> si tenía stun
        /// activo — es la única vía por la que un listener se enteraría de la cura.
        /// </summary>
        void Clear(Guid entity);

        /// <summary>
        /// Limpia el stun de todas las entidades sin disparar <c>OnStunExpired</c> (teardown,
        /// igual criterio que <c>ComboBlockService.Clear</c>). Corre en <c>OnCombatEnd</c> /
        /// <c>OnRunEnd</c>.
        /// </summary>
        void ClearAll();
    }
}
