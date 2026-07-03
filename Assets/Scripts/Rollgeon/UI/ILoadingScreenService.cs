using System;

namespace Rollgeon.UI
{
    /// <summary>
    /// Servicio global (<see cref="Patterns.ServiceScope.Global"/>) que muestra el
    /// spinner + fondo de carga persistente. Registrado por
    /// <see cref="LoadingScreenBootstrap"/>.
    /// </summary>
    public interface ILoadingScreenService
    {
        /// <param name="onRevealComplete">Callback opcional al terminar el reveal
        /// (ej. pushear la siguiente screen). Null = no hace nada extra.</param>
        void Show(Action onRevealComplete = null);

        /// <param name="progress01">0 = arranca, 1 = completo (dispara el reveal
        /// automáticamente).</param>
        void ReportProgress(float progress01);
    }
}
