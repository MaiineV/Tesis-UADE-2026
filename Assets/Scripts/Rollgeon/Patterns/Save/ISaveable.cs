namespace Patterns.Save
{
    /// <summary>
    /// Superficie mínima del contrato <c>ISaveable</c> descripto en TECHNICAL.md §15 (Save System).
    /// <para>
    /// <b>[STUB]</b> — el Save System real no está implementado en Sprint 03. Se expone esta
    /// interface para que los services run-scoped (p.ej. <see cref="Rollgeon.Combos.Counters.RunComboCounterState"/>)
    /// la implementen desde ya. Cuando el SaveSystem worktree la defina "final", las firmas
    /// se reconcilian — <c>SaveKey</c>, <c>CaptureState</c> y <c>RestoreState</c> coinciden
    /// con §15.
    /// </para>
    /// <para>
    /// <b>Riesgo de colisión.</b> Si otro worktree concurrente también stubea esta interface,
    /// el primero que mergea gana y los restantes reutilizan. No se duplica — si ya existe,
    /// este archivo debe borrarse durante el merge.
    /// </para>
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Clave estable para el contenedor de save (p.ej. <c>"run.combo_counter_state"</c>).</summary>
        string SaveKey { get; }

        /// <summary>Serializa el estado actual a un objeto round-trippable (dict, list, struct).</summary>
        object CaptureState();

        /// <summary>Restaura el estado desde el objeto producido por <see cref="CaptureState"/>.</summary>
        void RestoreState(object state);
    }
}
