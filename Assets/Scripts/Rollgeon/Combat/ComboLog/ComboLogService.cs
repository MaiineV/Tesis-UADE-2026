using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.ComboLog
{
    /// <summary>
    /// Implementación POCO de <see cref="IComboLogService"/> (Sistemas prerequisito Bosses §3).
    /// Buffer acotado de los combos ejecutados, más reciente al frente. Mismo patrón de lifecycle
    /// que <c>ComboBlockService</c>: registro global, state run-scoped (clear en OnCombatEnd/OnRunEnd).
    /// </summary>
    public sealed class ComboLogService : IComboLogService, IPreloadableService, IDisposable
    {
        /// <summary>Capacidad máxima del buffer. La ventana de lectura (1/2) es mucho menor; este
        /// tope solo evita crecimiento ilimitado en combates largos.</summary>
        private const int MaxHistory = 16;

        private const string DefaultNoComboMarker = "combo.none";

        // _history[0] es el más reciente. Lazy por la misma razón que ComboBlockService.
        private List<string> _history;
        private List<string> History => _history ??= new List<string>(MaxHistory);

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        public int Priority => 80;

        public string NoComboMarker => DefaultNoComboMarker;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);

            ServiceLocator.AddService<IComboLogService>(this, ServiceScope.Global);
        }

        public void Dispose()
        {
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
            History.Clear();
        }

        // ======================================================================
        // IComboLogService
        // ======================================================================

        /// <inheritdoc />
        public void Record(string comboId)
        {
            var value = string.IsNullOrEmpty(comboId) ? DefaultNoComboMarker : comboId;
            History.Insert(0, value);
            if (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        }

        /// <inheritdoc />
        public string LastCombo => History.Count > 0 ? History[0] : null;

        /// <inheritdoc />
        public IReadOnlyList<string> Last(int count)
        {
            if (count <= 0 || History.Count == 0) return Array.Empty<string>();
            int n = count < History.Count ? count : History.Count;
            var result = new string[n];
            for (int i = 0; i < n; i++) result[i] = History[i];
            return result;
        }

        /// <inheritdoc />
        public void Clear() => History.Clear();

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnScopeEndedExternal(params object[] args) => Clear();
    }
}
