using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Implementacion runtime (clase plana, NO <see cref="MonoBehaviour"/>) del
    /// presupuesto de rerolls. TECHNICAL.md §6.5. Plan Feature#0104.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lifecycle.</b> <see cref="Register"/> lo invoca <c>ServiceBootstrapSO</c>
    /// en el bootstrap global (Foundation#0005); el servicio se registra a si mismo
    /// bajo <see cref="IRerollBudgetService"/>. <see cref="Priority"/> <c>= 70</c>
    /// — despues de <see cref="EnergyService"/> (50) y <see cref="TurnManager"/> (60),
    /// que son prerequisitos logicos del runtime de combate.
    /// </para>
    /// <para>
    /// <b>Tuning.</b> Hoy el costo en energia de un paid reroll es un constante
    /// <see cref="DefaultEnergyCostPerExtraReroll"/> = 1. Si <c>RulesetSO</c> publica
    /// un campo <c>EnergyCostPerExtraReroll</c> en el futuro (Sprint 04 / Balance#0101),
    /// el servicio lo leera automaticamente. Plan §6.2.
    /// </para>
    /// </remarks>
    public sealed class RerollBudgetService : IRerollBudgetService, IPreloadableService, IDisposable
    {
        // ======================================================================
        // BlockedReason tag constants (estables — HUD puede key-off para locales)
        // ======================================================================

        /// <summary>No hay presupuesto abierto: <c>StartBudget</c> nunca se llamo o ya se cerro.</summary>
        public const string BlockedReasonNoActiveBudget = "no-active-budget";

        /// <summary>No quedan tiradas libres y la accion no permite paid rerolls.</summary>
        public const string BlockedReasonActionForbidsEnergyReroll = "action-forbids-energy-reroll";

        /// <summary>No quedan tiradas libres y el player no tiene energia suficiente.</summary>
        public const string BlockedReasonNoEnergy = "no-energy";

        // ======================================================================
        // Tuning fallback
        // ======================================================================

        /// <summary>
        /// Costo default de un paid reroll cuando <c>RulesetSO</c> no expone el knob.
        /// Plan §6.2 — TODO(sprint04) leer de <c>RulesetSO.EnergyCostPerExtraReroll</c>
        /// apenas el campo exista.
        /// </summary>
        public const int DefaultEnergyCostPerExtraReroll = 1;

        // ======================================================================
        // State
        // ======================================================================

        private IEnergyService _energy;
        private RulesetSO _ruleset; // opcional — leer-si-existe para tuning.

        private RerollBudget _current;

        // BUG-019: free rolls que quedaron sin usar del último budget terminado en
        // ESTE turno. La acción de defensa (Special Attack) comparte pool con Attack:
        // si Attack tenía 3 rolls free y el user usó 2, defensa empieza con 1.
        // Se resetea a -1 al inicio de cada turno (sentinel = "sin budget previo").
        private int _lastEndedBudgetRollsRemaining = -1;
        private EventManager.EventReceiver _onTurnStartedHandler;

        /// <inheritdoc />
        public RerollBudget Current => _current;

        /// <summary>
        /// BUG-019: free rolls que quedaron sin usar al cerrar el último budget en
        /// este turno. <c>-1</c> = todavía no terminó ningún budget. La acción de
        /// defensa lee este valor para gatear el botón y para inicializar su propio
        /// budget con el remanente del ataque.
        /// </summary>
        public int LastEndedBudgetRollsRemaining => _lastEndedBudgetRollsRemaining;

        /// <inheritdoc />
        public event Action<RerollStartedPayload> OnRerollStarted;

        /// <inheritdoc />
        public event Action<RerollBudget> OnBudgetStarted;

        /// <summary>Despues de EnergyService (50) y TurnManager (60).</summary>
        public int Priority => 70;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            if (!ServiceLocator.TryGetService<IEnergyService>(out _energy) || _energy == null)
            {
                Debug.LogError("[RerollBudgetService] IEnergyService no esta registrado en ServiceLocator. " +
                               "Agregar EnergyServiceBootstrap (o equivalente) con Priority < 70.");
                return;
            }

            // RulesetSO es opcional: si no esta, usamos defaults constantes.
            ServiceLocator.TryGetService<RulesetSO>(out _ruleset);

            // BUG-019: el carryover de rolls (Attack→Defense) se resetea por turno.
            _onTurnStartedHandler = OnTurnStartedExternal;
            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);

            ServiceLocator.AddService<IRerollBudgetService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            OnRerollStarted = null;
            OnBudgetStarted = null;
            _current = null;
            _energy = null;
            _ruleset = null;
            if (_onTurnStartedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
                _onTurnStartedHandler = null;
            }
        }

        // BUG-019: OnTurnStarted schema [Guid entityGuid] — limpiamos el carryover
        // sin filtrar por entityGuid (el RerollBudget es del player actual del HUD;
        // si el turno cambia a un enemy el carryover deja de ser relevante igual).
        private void OnTurnStartedExternal(params object[] args)
        {
            _lastEndedBudgetRollsRemaining = -1;
        }

        // ======================================================================
        // Test / dependency injection hook
        // ======================================================================

        /// <summary>
        /// Hook para EditMode tests — inyecta dependencias sin pasar por
        /// <see cref="ServiceLocator"/>. <paramref name="ruleset"/> es opcional.
        /// </summary>
        public void ConfigureForTests(IEnergyService energy, RulesetSO ruleset = null)
        {
            _energy = energy;
            _ruleset = ruleset;
        }

        // ======================================================================
        // IRerollBudgetService
        // ======================================================================

        /// <inheritdoc />
        public void StartBudget(ActionDefinitionSO action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (_current != null)
            {
                throw new InvalidOperationException(
                    $"[RerollBudgetService] StartBudget called while a budget is already active " +
                    $"(Action='{_current.Action?.ActionId}'). Call EndBudget first.");
            }

            // El primer roll tambien consume del budget (flow manual: el usuario
            // aprieta Roll para gatillar la primera tirada). Por eso el budget
            // cuenta el total — el contador de UI muestra "rolls disponibles".
            int freeRolls = action.FreeRollCount;
            if (freeRolls < 0) freeRolls = 0;

            _current = new RerollBudget
            {
                Action = action,
                FreeRollsRemaining = freeRolls,
                PaidRollsUsed = 0,
            };

            OnBudgetStarted?.Invoke(_current);
        }

        /// <inheritdoc />
        public void EndBudget()
        {
            if (_current == null) return;
            // BUG-019: snapshot del remanente ANTES del Reset, para que la acción
            // siguiente (típicamente defensa post-attack) lea cuánto pool quedó.
            _lastEndedBudgetRollsRemaining = Math.Max(0, _current.FreeRollsRemaining);
            _current.Reset();
            _current = null;
        }

        /// <inheritdoc />
        public RerollQueryResult QueryExtraRoll(Guid playerGuid)
        {
            if (_current == null)
            {
                return RerollQueryResult.Blocked(BlockedReasonNoActiveBudget);
            }

            if (_current.FreeRollsRemaining > 0)
            {
                return RerollQueryResult.Free();
            }

            if (_current.Action == null || !_current.Action.AllowsEnergyReroll)
            {
                return RerollQueryResult.Blocked(BlockedReasonActionForbidsEnergyReroll);
            }

            int cost = GetEnergyCostPerExtraReroll();
            if (_energy == null)
            {
                return RerollQueryResult.Blocked(BlockedReasonNoEnergy);
            }

            int available = _energy.GetCurrent(playerGuid);
            if (available < cost)
            {
                return RerollQueryResult.Blocked(BlockedReasonNoEnergy);
            }

            return RerollQueryResult.Paid();
        }

        /// <inheritdoc />
        public bool TryExtraRoll(Guid playerGuid)
        {
            if (_current == null)
            {
                Debug.LogError("[RerollBudgetService] TryExtraRoll called with no active budget. " +
                               "Caller should have invoked StartBudget on action start.");
                return false;
            }

            // 1) Path gratis.
            if (_current.ConsumeFree())
            {
                FireRerollStarted(playerGuid, isFree: true);
                return true;
            }

            // 2) Paid path — gated por flag de la accion.
            var action = _current.Action;
            if (action == null || !action.AllowsEnergyReroll)
            {
                return false;
            }

            // 3) Cobrar energia. Si falla (no-energy / service rechaza), no mutamos.
            if (_energy == null) return false;
            int cost = GetEnergyCostPerExtraReroll();
            if (!_energy.SpendEnergy(playerGuid, cost)) return false;

            // 4) Bookkeeping + evento.
            _current.ConsumePaid();
            FireRerollStarted(playerGuid, isFree: false);
            return true;
        }

        // ======================================================================
        // Helpers privados
        // ======================================================================

        private void FireRerollStarted(Guid playerGuid, bool isFree)
        {
            var payload = new RerollStartedPayload(
                playerGuid: playerGuid,
                action: _current?.Action,
                isFree: isFree,
                freeRollsRemaining: _current?.FreeRollsRemaining ?? 0,
                paidRollsUsed: _current?.PaidRollsUsed ?? 0);

            // 1) Callback tipado (suscriptores internos del juego, HUD, tests).
            OnRerollStarted?.Invoke(payload);

            // 2) Bus legacy — compat con el schema documentado de EventName.OnRerollStarted
            //    (TECHNICAL.md §1.2 reroll section). Schema legacy:
            //    [Guid sourceGuid, int rerollIndex]. rerollIndex = 1-based index del reroll
            //    consumido (gratis o pago, sumados). Los suscriptores nuevos deberian
            //    migrar al callback tipado.
            int rerollIndex = ComputeRerollIndex();
            EventManager.Trigger(EventName.OnRerollStarted, playerGuid, rerollIndex);
        }

        /// <summary>
        /// 1-based index del roll recien consumido = (free consumidos + paid consumidos).
        /// El primer roll = 1, el primer reroll = 2, etc. Asume que esta funcion se
        /// llama <b>despues</b> de <c>ConsumeFree</c>/<c>ConsumePaid</c>.
        /// </summary>
        private int ComputeRerollIndex()
        {
            if (_current == null) return 0;
            int total = _current.Action != null ? _current.Action.FreeRollCount : 0;
            if (total < 0) total = 0;
            int freeConsumed = total - _current.FreeRollsRemaining;
            if (freeConsumed < 0) freeConsumed = 0;
            return freeConsumed + _current.PaidRollsUsed;
        }

        private int GetEnergyCostPerExtraReroll()
        {
            // [FOLLOWUP sprint04 / Balance#0101]: si RulesetSO agrega
            // EnergyCostPerExtraReroll, leerlo aca con el fallback a la constante.
            // if (_ruleset != null) return _ruleset.Rolls.EnergyCostPerExtraReroll;
            return DefaultEnergyCostPerExtraReroll;
        }
    }
}
