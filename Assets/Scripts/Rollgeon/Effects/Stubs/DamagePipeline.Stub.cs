using UnityEngine;

namespace Rollgeon.Effects.Stubs
{
    /// <summary>
    /// [STUB] — reemplazado por System#0100b / T103 (pipeline de daño unificado,
    /// TECHNICAL.md §12). En esta foundation el stub sólo emite <see cref="Debug.Log"/>
    /// para que el effect ejemplar <c>EffDamage</c> complete el flujo sin introducir
    /// dependencias que todavía no existen (crit, mitigación, elementales, …).
    /// </summary>
    public static class DamagePipelineStub
    {
        /// <summary>
        /// Aplica <paramref name="amount"/> de daño de <paramref name="source"/> a
        /// <paramref name="target"/>. En el stub sólo loggea. El pipeline real resuelve
        /// mitigación → critico → elementales → escribe en <c>IAttribute&lt;int&gt; Health</c>.
        /// </summary>
        public static void Apply(Entity source, Entity target, int amount)
        {
            var srcGuid = source != null ? source.Guid.ToString() : "null";
            var tgtGuid = target != null ? target.Guid.ToString() : "null";
            Debug.Log($"[STUB DamagePipeline] {srcGuid} → {tgtGuid}: {amount} damage");
        }
    }
}
