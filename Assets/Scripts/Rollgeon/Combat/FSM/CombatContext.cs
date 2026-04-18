using System;
using System.Collections.Generic;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Energy;

namespace Rollgeon.Combat.FSM
{
    /// <summary>
    /// Contexto compartido entre los estados de <see cref="CombatTurnFSM"/>.
    /// Plan §4.1.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Inmutable tras construccion</b>, salvo <see cref="PendingOutcome"/> (set
    /// por <see cref="CombatController.NotifyCombatEnded"/>) y
    /// <see cref="CachedParticipants"/> (set por
    /// <see cref="CombatTurnFSM.SetParticipants"/> antes de <c>Start</c>).
    /// </para>
    /// <para>
    /// <b>Servicios por referencia, no por construccion.</b> No instanciamos los
    /// servicios aca — el <see cref="CombatController"/> los resuelve del
    /// <see cref="Patterns.ServiceLocator"/> y los pasa.
    /// </para>
    /// </remarks>
    public sealed class CombatContext
    {
        /// <summary>Servicio de orden de turno (T100c).</summary>
        public TurnOrderService TurnOrder { get; }

        /// <summary>Servicio de action economy / repetition constraint (T100b). Puede ser null en tests sin action dispatch.</summary>
        public TurnManager TurnManager { get; }

        /// <summary>Servicio de energia (T100a). Consumido por tests para validar regen / gasto.</summary>
        public IEnergyService Energy { get; }

        /// <summary>Guid del player activo.</summary>
        public Guid PlayerId { get; }

        /// <summary>
        /// Instance del room donde se pelea. Payload de <c>OnCombatStart</c> /
        /// <c>OnCombatEnd</c>.
        /// </summary>
        public Guid RoomInstanceId { get; }

        /// <summary>
        /// Delegate inyectado por la scene / test que decide que hace el enemy
        /// en su turno. Debe llamar <see cref="CombatController.SendInput"/> con
        /// <see cref="CombatInput.EnemyDone"/> cuando termine.
        /// </summary>
        /// <remarks>
        /// [STUB] — T99/T103 provideran la AI real via inyeccion. Para el FP,
        /// un test/driver puede devolver <c>true</c> sincronamente indicando
        /// "ya termine, dispara EnemyDone". El contract es: firma de conveniencia.
        /// El <see cref="EnemyTurnState"/> invoca este delegate y acepta que el
        /// handler dispare <c>EnemyDone</c> reentrant (la FSM encola inputs).
        /// </remarks>
        public Action<Guid> EnemyActionHandler { get; }

        /// <summary>
        /// Outcome que <see cref="CombatExitState"/> lee al entrar. Setearlo desde
        /// <see cref="CombatController.NotifyCombatEnded"/> antes de disparar
        /// <see cref="CombatInput.CombatEnded"/>.
        /// </summary>
        public CombatOutcome? PendingOutcome { get; set; }

        /// <summary>
        /// Lista cacheada de participantes. <see cref="CombatEnterState"/> la
        /// pasa a <c>TurnOrder.BuildForCombat</c>.
        /// </summary>
        public IReadOnlyList<Guid> CachedParticipants { get; set; }

        public CombatContext(
            TurnOrderService turnOrder,
            TurnManager turnManager,
            IEnergyService energy,
            Guid playerId,
            Guid roomInstanceId,
            Action<Guid> enemyActionHandler)
        {
            TurnOrder = turnOrder ?? throw new ArgumentNullException(nameof(turnOrder));
            // TurnManager puede ser null en tests unitarios de la FSM.
            TurnManager = turnManager;
            Energy = energy ?? throw new ArgumentNullException(nameof(energy));

            if (playerId == Guid.Empty)
            {
                throw new ArgumentException("PlayerId no puede ser Guid.Empty.", nameof(playerId));
            }
            PlayerId = playerId;
            RoomInstanceId = roomInstanceId;
            EnemyActionHandler = enemyActionHandler;
        }
    }
}
