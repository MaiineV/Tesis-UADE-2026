namespace Rollgeon.UI.Cursor
{
    /// <summary>
    /// Servicio global del cursor custom. Registrado en <c>ServiceLocator</c>
    /// (Global) por <see cref="CursorBootstrap"/>.
    /// </summary>
    public interface ICursorService
    {
        /// <summary>
        /// Muestra u oculta el cursor custom (ej. durante un video/cutscene).
        /// Al ocultarlo se restaura el cursor del sistema.
        /// </summary>
        void SetVisible(bool visible);
    }
}
