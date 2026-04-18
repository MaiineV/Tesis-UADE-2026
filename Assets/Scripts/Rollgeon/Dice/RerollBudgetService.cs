using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.Energy;
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

        /// <inheritdoc />
        public RerollBudget Current => _current;

        /// <inheritdoc />
        public event Action<RerollStartedPayload> OnRerollStarted;

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

            ServiceLocator.AddService<IRerollBudgetService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            OnRerollStarted = null;
            _current = null;
            _energy = null;
            _ruleset = null;
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

            // Convention: FreeRollCount cuenta total rolls incl. primero.
            // El budget cuenta RE-rolls, de ahi el "-1".
            int freeRerolls = action.FreeRollCount - 1;
            if (freeRerolls < 0) freeRerolls = 0;

            _current = new RerollBudget
            {
                Action = action,
                FreeRollsRemaining = freeRerolls,
                PaidRollsUsed = 0,
            };
        }

        /// <inheritdoc />
        public void EndBudget()
        {
            if (_current == null) return;
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
        /// 1-based index del reroll recien consumido = free consumidos + paid consumidos.
        /// Asume que esta funcion se llama <b>despues</b> de <c>ConsumeFree</c>/<c>ConsumePaid</c>.
        /// </summary>
        private int ComputeRerollIndex()
        {
            if (_current == null) return 0;
            int initialFree = 0;
            if (_current.Action != null)
            {
                int v = _current.Action.FreeRollCount - 1;
                initialFree = v < 0 ? 0 : v;
            }
            int freeConsumed = initialFree - _current.FreeRollsRemaining;
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
