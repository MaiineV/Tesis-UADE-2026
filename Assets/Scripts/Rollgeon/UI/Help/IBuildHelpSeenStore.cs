namespace Rollgeon.UI.Help
{
    /// <summary>
    /// Recuerda si el jugador ya vio la guía de armado de bolsa. Existe como interfaz
    /// para que el auto-disparo sea testeable sin tocar PlayerPrefs (estado global del
    /// proceso, prohibido en tests) y para dejar la decisión de dónde persistir
    /// intercambiable.
    /// </summary>
    public interface IBuildHelpSeenStore
    {
        bool HasSeen { get; }
        void MarkSeen();
        void Clear();
    }
}
