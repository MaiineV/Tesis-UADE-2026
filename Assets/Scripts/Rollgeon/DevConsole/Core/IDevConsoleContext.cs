using System;

namespace Rollgeon.DevConsole.Core
{
    /// <summary>
    /// Único seam entre los comandos y el resto del juego. La impl real delega en
    /// <c>ServiceLocator</c>; los tests inyectan un fake. Los comandos NUNCA tocan
    /// <c>ServiceLocator</c> directo — así son 100% testeables.
    /// </summary>
    public interface IDevConsoleContext
    {
        bool TryResolve<T>(out T service);
        T Resolve<T>();
        ILogSink Log { get; }
        bool IsRunActive { get; }
        Guid PlayerGuid { get; }
    }
}
