namespace Patterns.Save
{
    /// <summary>
    /// Contrato de persistencia del Save System (TECHNICAL.md §15). Los saveables se
    /// registran en <see cref="SaveSystem"/> — creación → <c>Register</c>, disposal →
    /// <c>Unregister</c> — y el sistema captura/restaura vía este contrato.
    /// <para>
    /// <b>Rehidratación no-trivial.</b> Un ISaveable que contenga objetos que requieren
    /// re-wire de eventos tras restore (canónicamente <c>Modifier&lt;T&gt;</c>, §3.5)
    /// debe re-hookearlos dentro de su <see cref="RestoreState"/> — <c>SaveSystem</c>
    /// no conoce los tipos concretos.
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
