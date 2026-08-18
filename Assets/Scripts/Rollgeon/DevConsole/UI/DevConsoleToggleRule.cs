namespace Rollgeon.DevConsole.UI
{
    /// <summary>
    /// Decide si la tecla <c>P</c> está actuando como toggle de la consola o como una letra más del
    /// comando que se está escribiendo. C# puro para poder testear la regla sin Unity.
    /// </summary>
    /// <remarks>
    /// El backquote y F1 pueden ser toggle sin ambigüedad porque no son letras. La P sí lo es, así
    /// que necesita una regla que diga cuándo gana el toggle. La regla es <b>el campo vacío</b>:
    /// con el input en blanco no hay comando a medio escribir, así que la P sólo puede significar
    /// "cerrá esto"; apenas hay un carácter, la P vuelve a ser texto.
    /// <para>
    /// El precio es <c>potion</c>, el único de los ~30 comandos que empieza con p: abriendo con P no
    /// se puede tipear su primera letra. Se sigue pudiendo con ` o F1, que no arman esta regla.
    /// </para>
    /// <para>
    /// Que dependa de <paramref name="openedWithP"/> y no sólo del campo vacío es a propósito: quien
    /// abre con ` o F1 nunca pidió que la P fuera una tecla especial, y para esa sesión la letra
    /// funciona siempre.
    /// </para>
    /// </remarks>
    public static class DevConsoleToggleRule
    {
        /// <summary>
        /// <c>true</c> si la P tiene que cerrar la consola en vez de escribirse en el campo.
        /// </summary>
        /// <param name="openedWithP">La consola abierta se abrió con la P (no con ` ni F1).</param>
        /// <param name="currentInput">Lo que hay tipeado en el campo de comando ahora mismo.</param>
        public static bool PIsTheToggle(bool openedWithP, string currentInput)
            => openedWithP && string.IsNullOrEmpty(currentInput);
    }
}
