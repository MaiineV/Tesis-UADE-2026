using System.Collections.Generic;

namespace Rollgeon.Combat.ComboBlock
{
    /// <summary>
    /// Servicio run-scoped que administra los combos bloqueados por el Boss Floor Manager
    /// (Content#0103). Plan §4.2. La implementacion concreta se registra como
    /// <c>IPreloadableService</c> en Global (la state interna si es run-scoped; ver
    /// <see cref="Clear"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Semantica.</b>
    /// <list type="bullet">
    ///   <item><description><c>Block(null | "" | duration &lt;= 0)</c> → no-op, sin evento.</description></item>
    ///   <item><description><c>Block(id, d)</c> donde <c>id</c> ya esta bloqueado → toma el max de las duraciones.</description></item>
    ///   <item><description><c>Block</c> dispara <c>EventName.OnComboBlocked(comboId, durationTurns)</c>.</description></item>
    ///   <item><description><c>TickDuration</c>: decrementa cada entry; las que llegan a 0 se remueven y disparan <c>OnComboUnblocked(comboId)</c>.</description></item>
    ///   <item><description><c>Clear</c>: vacia el dict sin disparar eventos.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Hook.</b> El concrete se suscribe a <c>EventName.OnTurnFinished</c> (CombatTurnFSM —
    /// T100d) y invoca <see cref="TickDuration"/> solo cuando el Guid del turno terminado
    /// coincide con el <c>PlayerGuid</c> resuelto via <c>IPlayerService</c>.
    /// </para>
    /// </remarks>
    public interface IComboBlockService
    {
        /// <summary>
        /// Bloquea <paramref name="comboId"/> por <paramref name="durationTurns"/> turnos del
        /// jugador. Si ya estaba bloqueado, toma el <c>max</c> de las duraciones. No-op si
        /// <paramref name="comboId"/> es null/empty o <paramref name="durationTurns"/> &lt;= 0.
        /// </summary>
        void Block(string comboId, int durationTurns);

        /// <summary><c>true</c> si <paramref name="comboId"/> esta bloqueado (remaining &gt; 0).</summary>
        bool IsBlocked(string comboId);

        /// <summary>Turnos restantes de bloqueo para <paramref name="comboId"/>; 0 si no esta bloqueado.</summary>
        int GetRemainingTurns(string comboId);

        /// <summary>
        /// Decrementa la duracion de todos los bloqueos; dispara <c>OnComboUnblocked</c> para
        /// los que llegan a 0. Llamado desde el handler de <c>OnTurnFinished</c> filtrado por
        /// el player (o manualmente por tests).
        /// </summary>
        void TickDuration();

        /// <summary>Vacia el diccionario. No dispara eventos. Usado en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.</summary>
        void Clear();

        /// <summary>Vista read-only del dict interno para HUD / tests.</summary>
        IReadOnlyDictionary<string, int> ActiveBlocks { get; }
    }
}
