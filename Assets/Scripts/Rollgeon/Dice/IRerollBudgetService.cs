using System;
using Patterns;
using Rollgeon.Combat.Actions;

namespace Rollgeon.Dice
{
    /// <summary>
    /// API publica del servicio de reroll budget. TECHNICAL.md §6.5 — superset del
    /// contrato <c>IRerollBudget</c> original (ver plan §4.3 y Appendix B).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Consumidores.</b>
    /// <list type="bullet">
    ///   <item><c>DiceRoller</c> (§6.3) — via adapter <c>IRerollBudget</c> (T95b).</item>
    ///   <item><c>CombatHUD</c> (T95b) — renderiza boton "Extra roll (1E)".</item>
    ///   <item>Secondary action handlers (§12.5) — heal y force-door comparten el budget.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Single budget.</b> El servicio mantiene un unico <see cref="Current"/> activo
    /// — single-player, single-active-action. Para multiplayer / AI concurrente se
    /// refactoriza a dict por Guid (plan §10.6); hoy las firmas ya estan parametrizadas
    /// por <see cref="Guid"/> para que el cambio sea contenido.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> Implementaciones son <see cref="Rollgeon.Patterns.Bootstrap.IPreloadableService"/> —
    /// registradas en <c>ServiceBootstrapSO.ExtraServices</c>. Ver
    /// <c>docs/setup/Feature#0104_EnergyReroll.md</c>.
    /// </para>
    /// </remarks>
    public interface IRerollBudgetService
    {
        /// <summary>
        /// Presupuesto activo en este momento. <c>null</c> antes de <see cref="StartBudget"/>
        /// y despues de <see cref="EndBudget"/>.
        /// </summary>
        RerollBudget Current { get; }

        /// <summary>
        /// Evento que levanta el servicio tras cada reroll concedido (gratis o pago).
        /// Se dispara <b>despues</b> del bookkeeping — los handlers ven post-spend state.
        /// Payload: <see cref="RerollStartedPayload"/>.
        /// </summary>
        event Action<RerollStartedPayload> OnRerollStarted;

        /// <summary>
        /// Abre un presupuesto nuevo para <paramref name="action"/>. Inicializa
        /// <c>FreeRollsRemaining = max(0, action.FreeRollCount - 1)</c> (convention:
        /// <c>FreeRollCount</c> cuenta el total de tiradas incluyendo la inicial —
        /// el budget cuenta <b>re-rolls</b>, por eso restamos 1). Plan §5.1 y §6.1.
        /// </summary>
        /// <exception cref="InvalidOperationException">Si ya hay un presupuesto activo.
        /// El caller debe invocar <see cref="EndBudget"/> antes.</exception>
        /// <exception cref="ArgumentNullException">Si <paramref name="action"/> es null.</exception>
        void StartBudget(ActionDefinitionSO action);

        /// <summary>
        /// Cierra el presupuesto activo y libera el estado. Idempotente — no lanza
        /// si no habia presupuesto abierto.
        /// </summary>
        void EndBudget();

        /// <summary>
        /// Query puro (sin side effects): evalua si el siguiente reroll es gratis,
        /// pago o bloqueado. Consulta al <c>IEnergyService</c> para la disponibilidad
        /// de energia. Devuelve <see cref="RerollQueryResult.Blocked(string)"/> con
        /// <c>"no-active-budget"</c> si no hay presupuesto abierto.
        /// </summary>
        RerollQueryResult QueryExtraRoll(Guid playerGuid);

        /// <summary>
        /// Intenta conceder un reroll. Si hay tirada gratis la consume sin tocar
        /// energia. Si no hay y la accion permite energy-reroll, intenta cobrar 1
        /// punto de energia via <c>IEnergyService.SpendEnergy</c> y registra
        /// <see cref="RerollBudget.PaidRollsUsed"/>. Dispara
        /// <see cref="OnRerollStarted"/> solo si el reroll fue concedido.
        /// </summary>
        /// <returns>
        /// <c>true</c> si se concedio un reroll; <c>false</c> si estaba bloqueado
        /// (no hay budget / no-energy / accion no permite paid reroll / sin free y sin energy).
        /// </returns>
        bool TryExtraRoll(Guid playerGuid);
    }

    /// <summary>
    /// Payload del <see cref="IRerollBudgetService.OnRerollStarted"/> — snapshot
    /// <b>post</b> bookkeeping. Ver plan §4.4.
    /// </summary>
    public readonly struct RerollStartedPayload
    {
        /// <summary>Actor al que se le concedio el reroll.</summary>
        public readonly Guid PlayerGuid;

        /// <summary>Accion para la que se abrio el budget (<see cref="RerollBudget.Action"/>).</summary>
        public readonly ActionDefinitionSO Action;

        /// <summary>true si el reroll fue gratis; false si costo 1 de energia.</summary>
        public readonly bool IsFree;

        /// <summary>Snapshot post-consumo de <see cref="RerollBudget.FreeRollsRemaining"/>.</summary>
        public readonly int FreeRollsRemaining;

        /// <summary>Snapshot post-consumo de <see cref="RerollBudget.PaidRollsUsed"/>.</summary>
        public readonly int PaidRollsUsed;

        public RerollStartedPayload(Guid playerGuid, ActionDefinitionSO action, bool isFree,
            int freeRollsRemaining, int paidRollsUsed)
        {
            PlayerGuid = playerGuid;
            Action = action;
            IsFree = isFree;
            FreeRollsRemaining = freeRollsRemaining;
            PaidRollsUsed = paidRollsUsed;
        }
    }
}
