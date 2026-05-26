namespace Rollgeon.UI
{
    /// <summary>
    /// Marker interface for screen payloads passed through <see cref="IScreenManager.Push{TScreen}"/>
    /// and <see cref="IScreenManager.PushByStringId"/>. Plan §4 / TECHNICAL.md §17.D.
    /// <para>
    /// El MVP de T102 no declara payloads concretos (MainMenuScreen no los necesita). Se mantiene
    /// el tipo por type-safety: callers futuros (T95b CombatScreen, Reward, etc.) declararan
    /// <c>class XxxPayload : IScreenPayload</c> en su propio namespace.
    /// </para>
    /// </summary>
    public interface IScreenPayload
    {
    }
}
