namespace Rollgeon.UI
{
    /// <summary>
    /// Interface minima del stack de screens para el MVP de T102. Plan §4.1.
    /// </summary>
    /// <remarks>
    /// Superficie reducida vs TECHNICAL.md §17.D.2 (que usa <c>ScreenId</c> enum, <c>IsPaused</c>,
    /// <c>Replace</c>, events). El upgrade es aditivo — ver plan §10 R2.
    /// <para>
    /// <b>Overlays.</b> <see cref="PushOverlay{TScreen}"/> y <see cref="PopOverlay"/> existen para
    /// satisfacer el contrato del brief, pero en el MVP son <b>alias</b> de <see cref="Push{TScreen}"/>
    /// / <see cref="PopCurrent"/>. No hay stack de overlays separado todavia — plan §10 R8.
    /// </para>
    /// </remarks>
    public interface IScreenManager
    {
        /// <summary>Screen que esta al top del stack, o <c>null</c> si el stack esta vacio.</summary>
        IBaseScreen Current { get; }

        /// <summary>
        /// Pushea la screen registrada bajo el tipo <typeparamref name="TScreen"/>. Si no esta
        /// registrada, loggea warning y no modifica estado (fallback graceful, plan §10 R1).
        /// </summary>
        /// <typeparam name="TScreen">Tipo concreto de la screen. Debe estar registrado via
        /// <see cref="RegisterScreen"/>.</typeparam>
        /// <param name="payload">Payload opcional. Null si no aplica.</param>
        void Push<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen;

        /// <summary>
        /// Pushea la screen registrada bajo el string id <paramref name="screenId"/>. Escape hatch
        /// para navegar a screens cuyo tipo no se conoce en compile-time (ej: T102 apuntando a
        /// T98 antes de que T98 mergee). Fallback graceful si no hay match.
        /// </summary>
        void PushByStringId(string screenId, IScreenPayload payload = null);

        /// <summary>
        /// Pop del top del stack. Desactiva la screen actual, re-activa la anterior. No-op con
        /// warning si el stack esta vacio.
        /// </summary>
        void PopCurrent();

        /// <summary>
        /// <b>MVP:</b> alias de <see cref="Push{TScreen}"/>. El split real de overlay stack queda
        /// para la tarea de ScreenManager completo (plan §10 R8).
        /// </summary>
        void PushOverlay<TScreen>(IScreenPayload payload = null) where TScreen : class, IBaseScreen;

        /// <summary>
        /// <b>MVP:</b> alias de <see cref="PopCurrent"/>. Ver <see cref="PushOverlay{TScreen}"/>.
        /// </summary>
        void PopOverlay();

        /// <summary>
        /// Indexa una screen por <see cref="System.Type"/> y por <see cref="IBaseScreen.ScreenStringId"/>.
        /// Llamado por <see cref="ScreenHost"/> en <c>Awake</c>.
        /// </summary>
        void RegisterScreen(IBaseScreen screen);

        /// <summary>
        /// Limpia ambos indices para la screen. Llamado por <see cref="ScreenHost"/> en <c>OnDestroy</c>.
        /// </summary>
        void UnregisterScreen(IBaseScreen screen);
    }
}
