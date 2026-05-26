namespace Patterns
{
    /// <summary>
    /// Scope de vida de un servicio registrado en <see cref="ServiceLocator"/>.
    /// Definido por TECHNICAL.md §1.1.
    /// </summary>
    public enum ServiceScope
    {
        /// <summary>Persiste toda la sesión (desde bootstrap hasta shutdown).</summary>
        Global,

        /// <summary>
        /// Persiste únicamente durante una run. Se limpia con
        /// <see cref="ServiceLocator.ClearScope(ServiceScope)"/> al terminar la run
        /// sin afectar a los servicios <see cref="Global"/>.
        /// </summary>
        Run,
    }
}
