namespace Rollgeon.Audio
{
    /// <summary>
    /// Contexto musical del juego — decide qué bucket de <see cref="MusicLibrarySO"/>
    /// suena. Lo resuelve <see cref="MusicDirector"/> a partir de los eventos de
    /// run/dungeon/combate.
    /// </summary>
    public enum MusicContext
    {
        MainMenu    = 0,
        Exploration = 1,
        Combat      = 2,
        Boss        = 3,
    }
}
