using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Player;
using Rollgeon.Run;

namespace Rollgeon.DevConsole.Core
{
    /// <summary>Contexto de producción: resuelve servicios contra el <see cref="ServiceLocator"/>.</summary>
    public sealed class ServiceLocatorConsoleContext : IDevConsoleContext
    {
        public ServiceLocatorConsoleContext(ILogSink log)
        {
            Log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public ILogSink Log { get; }

        public bool TryResolve<T>(out T service) => ServiceLocator.TryGetService(out service);

        public T Resolve<T>()
        {
            if (ServiceLocator.TryGetService<T>(out var service)) return service;
            throw new KeyNotFoundException($"Servicio no registrado: {typeof(T).Name}");
        }

        // Lazy: el run/player puede no existir aún (menú principal, bootstrap).
        public bool IsRunActive =>
            ServiceLocator.TryGetService<IRunController>(out var rc) && rc != null && rc.IsRunActive;

        public Guid PlayerGuid =>
            ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;
    }
}
