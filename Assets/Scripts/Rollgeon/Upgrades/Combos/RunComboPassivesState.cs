using System.Collections.Generic;

namespace Rollgeon.Upgrades.Combos
{
    /// <summary>
    /// Estado run-scoped del Canal Combos — diccionario de pasivas aplicadas por
    /// <see cref="ComboPassiveSO.TargetComboId"/>. Una run puede acumular varias
    /// pasivas del mismo combo (stacking).
    /// </summary>
    /// <remarks>
    /// Registrado en <c>ServiceScope.Run</c> al inicio de cada run vía
    /// <see cref="ComboPassiveService"/>. Auto-libera al fin de la run via
    /// <c>ClearScope(Run)</c>.
    /// </remarks>
    public sealed class RunComboPassivesState
    {
        private readonly Dictionary<string, List<ComboPassiveSO>> _passivesByCombo
            = new Dictionary<string, List<ComboPassiveSO>>();

        /// <summary>Suma una pasiva al stack del combo target. Permite repetidos.</summary>
        public void Add(ComboPassiveSO passive)
        {
            if (passive == null) return;
            if (string.IsNullOrEmpty(passive.TargetComboId)) return;

            if (!_passivesByCombo.TryGetValue(passive.TargetComboId, out var list))
            {
                list = new List<ComboPassiveSO>();
                _passivesByCombo[passive.TargetComboId] = list;
            }
            list.Add(passive);
        }

        /// <summary>Lista de pasivas activas para un combo. Empty si no hay ninguna.</summary>
        public IReadOnlyList<ComboPassiveSO> Get(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return System.Array.Empty<ComboPassiveSO>();
            return _passivesByCombo.TryGetValue(comboId, out var list)
                ? list
                : System.Array.Empty<ComboPassiveSO>();
        }

        /// <summary>Total de pasivas activas (todos los combos sumados). Diagnóstico / UI.</summary>
        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (var kv in _passivesByCombo) total += kv.Value.Count;
                return total;
            }
        }
    }
}
