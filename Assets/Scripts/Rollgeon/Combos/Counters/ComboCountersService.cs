using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combos.Counters
{
    /// <summary>
    /// Implementación runtime del sistema de <b>Combo Counters</b> (TECHNICAL.md §5.5).
    /// <para>
    /// Clase plana (no <see cref="MonoBehaviour"/>) registrada en <c>ServiceScope.Global</c>
    /// durante el bootstrap. Suscribe a <c>TypedEvent&lt;ComboMatchedPayload&gt;</c>
    /// (publicado por <c>AttackResolver</c> T100b) y bumpea el <see cref="RunComboCounterState"/>
    /// run-scoped.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b>
    /// <list type="bullet">
    /// <item><description><c>Register()</c> → se auto-registra bajo <see cref="IComboCountersService"/>
    /// y suscribe listeners de <c>OnRunStart</c>, <c>OnRunEnd</c> y
    /// <c>TypedEvent&lt;ComboMatchedPayload&gt;</c>.</description></item>
    /// <item><description><c>OnRunStart</c> → instancia un <see cref="RunComboCounterState"/> fresco
    /// y lo registra en <c>ServiceScope.Run</c>.</description></item>
    /// <item><description><c>OnRunEnd</c> → no-op (el state se libera por <c>ClearScope(Run)</c>
    /// en <see cref="BootstrapHooks"/>).</description></item>
    /// <item><description><c>TypedEvent&lt;ComboMatchedPayload&gt;</c> → <see cref="IncrementCount"/>
    /// y dispara <see cref="EventName.OnComboCounterIncremented"/>.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Out of run.</b> Fuera de una run (p.ej. main menu, class preview), el state no
    /// está registrado y el service degrada a <c>0</c> / <c>1.0f</c> / no-op. No crash.
    /// </para>
    /// </summary>
    public sealed class ComboCountersService : IComboCountersService, IPreloadableService, IDisposable
    {
        /// <summary>Prioridad estándar para services run-scoped — después de Energy (50), Turn (60), Reroll (70).</summary>
        public const int DefaultPriority = 80;

        private RulesetSO _ruleset;
        private bool _subscribed;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            // RulesetSO es opcional: si no está, la fórmula degrada con defaults hardcoded.
            ServiceLocator.TryGetService<RulesetSO>(out _ruleset);

            ServiceLocator.AddService<IComboCountersService>(this, ServiceScope.Global);

            SubscribeEvents();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            UnsubscribeEvents();
            _ruleset = null;
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>
        /// Hook para EditMode tests — inyecta el ruleset sin pasar por <see cref="ServiceLocator"/>.
        /// </summary>
        public void ConfigureForTests(RulesetSO ruleset)
        {
            _ruleset = ruleset;
        }

        /// <summary>Hook para tests — registra/desregistra los listeners sin pasar por <c>Register()</c>.</summary>
        public void SubscribeEventsForTests() => SubscribeEvents();

        /// <summary>Hook para tests — desregistra los listeners.</summary>
        public void UnsubscribeEventsForTests() => UnsubscribeEvents();

        // ====================================================================
        // Event wiring
        // ====================================================================

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.Subscribe(EventName.OnRunEnd, OnRunEndHandler);
            TypedEvent<ComboMatchedPayload>.Subscribe(OnComboMatched);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnRunStart, OnRunStartHandler);
            EventManager.UnSubscribe(EventName.OnRunEnd, OnRunEndHandler);
            TypedEvent<ComboMatchedPayload>.Unsubscribe(OnComboMatched);
            _subscribed = false;
        }

        // ====================================================================
        // Run lifecycle handlers
        // ====================================================================

        // Schema EventName.OnRunStart: args = [Guid runId, string rulesetId]
        private void OnRunStartHandler(params object[] args)
        {
            // Instancia un state fresco por run y lo inscribe en el scope Run.
            var state = new RunComboCounterState();
            ServiceLocator.AddService<RunComboCounterState>(state, ServiceScope.Run);
        }

        // Schema EventName.OnRunEnd: args = [Guid runId, RunOutcome outcome]
        // No-op — BootstrapHooks.OnRunEnd → ClearScope(Run) se encarga del teardown.
        // Definido para simetría con OnRunStart y para facilitar tests.
        private void OnRunEndHandler(params object[] args)
        {
            // Intencionalmente vacío. ClearScope(Run) libera el state.
        }

        // ====================================================================
        // ComboMatched handler
        // ====================================================================

        private void OnComboMatched(ComboMatchedPayload payload)
        {
            if (string.IsNullOrEmpty(payload.ComboId)) return;
            IncrementCount(payload.ComboId);
        }

        // ====================================================================
        // IComboCountersService
        // ====================================================================

        /// <inheritdoc />
        public int GetCount(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return 0;
            if (!ServiceLocator.TryGetService<RunComboCounterState>(out var state) || state == null) return 0;
            return state.Get(comboId);
        }

        /// <inheritdoc />
        public void IncrementCount(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return;
            if (!ServiceLocator.TryGetService<RunComboCounterState>(out var state) || state == null) return;

            int newCount = state.Increment(comboId);
            EventManager.Trigger(EventName.OnComboCounterIncremented, comboId, newCount);
        }

        /// <inheritdoc />
        public float GetBonusMultiplier(string comboId)
        {
            int count = GetCount(comboId);
            if (count <= 0) return 1f;

            var cfg = GetConfig();
            if (cfg == null) return 1f;
            return cfg.ComputeMultiplier(count);
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private ComboCountersConfig GetConfig()
        {
            // Si el ruleset se registró DESPUÉS de Register(), lo resolvemos lazy aquí.
            if (_ruleset == null)
            {
                ServiceLocator.TryGetService<RulesetSO>(out _ruleset);
            }
            return _ruleset != null ? _ruleset.Counters : null;
        }
    }
}
