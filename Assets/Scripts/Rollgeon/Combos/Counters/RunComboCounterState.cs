using System;
using System.Collections.Generic;
using Patterns.Save;

namespace Rollgeon.Combos.Counters
{
    /// <summary>
    /// POCO run-scoped que almacena la cuenta de matches por <c>ComboId</c>. TECHNICAL.md §5.5.
    /// <para>
    /// <b>Lifecycle.</b> Instanciado por <see cref="ComboCountersService"/> en <c>OnRunStart</c>,
    /// registrado en <c>ServiceLocator</c> bajo <see cref="Patterns.ServiceScope.Run"/>, y
    /// descartado automáticamente por <c>BootstrapHooks.OnRunEnd → ClearScope(Run)</c>.
    /// </para>
    /// <para>
    /// <b>Save.</b> Implementa <see cref="ISaveable"/> (stub — §15 no está en el sprint).
    /// <see cref="CaptureState"/> devuelve un clone del <see cref="Counts"/> para asegurar
    /// inmutabilidad del snapshot serializado. <see cref="RestoreState"/> acepta un
    /// <c>IDictionary&lt;string,int&gt;</c> (o <c>null</c> → reset).
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class RunComboCounterState : ISaveable
    {
        /// <summary>
        /// Clave estable para el contenedor de save (§15). Documentada en el brief de la tarea.
        /// </summary>
        public const string SaveKeyConst = "run.combo_counter_state";

        /// <summary>Dict interno <c>ComboId → count</c>. Se expone como <c>IReadOnlyDictionary</c> via <see cref="Snapshot"/>.</summary>
        public Dictionary<string, int> Counts = new Dictionary<string, int>();

        // ---------------------------------------------------------------- API
        // Métodos helpers — no los necesita el service (hace Dict direct access)
        // pero sirven para tests y para un eventual tool de debug.

        /// <summary>Lectura segura: <c>0</c> si no hay entry o la key es null/empty.</summary>
        public int Get(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return 0;
            return Counts.TryGetValue(comboId, out var v) ? v : 0;
        }

        /// <summary>Incrementa <paramref name="comboId"/> en <c>+1</c>. No-op si la key es null/empty.</summary>
        public int Increment(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return 0;
            Counts.TryGetValue(comboId, out var cur);
            cur += 1;
            Counts[comboId] = cur;
            return cur;
        }

        /// <summary>Limpia el dict. Usado por tests; el sistema real se libera por <c>ClearScope(Run)</c>.</summary>
        public void Reset()
        {
            Counts.Clear();
        }

        /// <summary>Snapshot read-only para UI / debug tools.</summary>
        public IReadOnlyDictionary<string, int> Snapshot => Counts;

        // ---------------------------------------------------------------- ISaveable

        /// <inheritdoc />
        public string SaveKey => SaveKeyConst;

        /// <inheritdoc />
        public object CaptureState()
        {
            // Clone para que el snapshot serializado no comparta referencia con el state vivo.
            return new Dictionary<string, int>(Counts);
        }

        /// <inheritdoc />
        public void RestoreState(object state)
        {
            Counts.Clear();
            if (state is IDictionary<string, int> dict)
            {
                foreach (var kvp in dict)
                {
                    if (string.IsNullOrEmpty(kvp.Key)) continue;
                    Counts[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
