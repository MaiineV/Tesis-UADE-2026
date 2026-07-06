using System;
using System.Collections.Generic;
using Patterns.Save;
using UnityEngine;

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
    /// <para>
    /// <b>Save (§15).</b> Persiste los <c>UpgradeId</c> (con repetidos — stacking) y
    /// rehidrata resolviéndolos contra el pool que inyecta el service. El restore
    /// solo repuebla el diccionario: los <c>StatGrants</c> de cada pasiva NO se
    /// re-aplican acá — viven como modifiers del player y los restaura el snapshot
    /// de atributos.
    /// </para>
    /// </remarks>
    public sealed class RunComboPassivesState : ISaveable, IDisposable
    {
        public const string SaveKeyConst = "run.combo_passives";
        private const string LogPrefix = "[RunComboPassivesState] ";

        private readonly Dictionary<string, List<ComboPassiveSO>> _passivesByCombo
            = new Dictionary<string, List<ComboPassiveSO>>();

        private readonly Func<string, ComboPassiveSO> _resolveById;

        public RunComboPassivesState(Func<string, ComboPassiveSO> resolveById = null)
        {
            _resolveById = resolveById;
        }

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
            if (string.IsNullOrEmpty(comboId)) return Array.Empty<ComboPassiveSO>();
            return _passivesByCombo.TryGetValue(comboId, out var list)
                ? list
                : (IReadOnlyList<ComboPassiveSO>)Array.Empty<ComboPassiveSO>();
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

        // ---------------------------------------------------------------- ISaveable

        public string SaveKey => SaveKeyConst;

        public object CaptureState()
        {
            var ids = new List<string>();
            foreach (var kv in _passivesByCombo)
            {
                foreach (var passive in kv.Value)
                {
                    if (passive != null && !string.IsNullOrEmpty(passive.UpgradeId))
                        ids.Add(passive.UpgradeId);
                }
            }
            return ids;
        }

        public void RestoreState(object state)
        {
            _passivesByCombo.Clear();
            if (state is not List<string> ids || ids.Count == 0) return;

            if (_resolveById == null)
            {
                Debug.LogWarning(LogPrefix + $"Save con {ids.Count} pasivas pero sin resolver " +
                                 "(pool no asignado en ComboPassiveBootstrap) — se descartan.");
                return;
            }

            foreach (var id in ids)
            {
                var passive = _resolveById(id);
                if (passive == null)
                {
                    Debug.LogWarning(LogPrefix + $"Pasiva '{id}' del save no existe en el pool — se descarta.");
                    continue;
                }
                Add(passive);
            }
        }

        // ---------------------------------------------------------------- IDisposable

        /// <summary>
        /// Invocado por <c>ClearScope(Run)</c>. El Unregister explícito evita que dos
        /// instancias con la misma SaveKey convivan en el registry entre runs.
        /// </summary>
        public void Dispose()
        {
            SaveSystem.Unregister(this);
        }
    }
}
