using System;
using Patterns;
using Rollgeon.Combat.Initiative;
using Rollgeon.Player;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// <see cref="IInitiativeProvider"/> del Tutorial Mode: el jugador SIEMPRE actúa
    /// primero. Decora al provider default (que sigue decidiendo el orden relativo
    /// de los enemigos) devolviendo <see cref="int.MaxValue"/> para el player y
    /// clampeando al resto un escalón abajo.
    /// </summary>
    /// <remarks>
    /// Se registra GLOBAL (el ServiceLocator es un diccionario plano por tipo, así
    /// que pisa la entry del bootstrap) — <c>TutorialFlowController.Dispose</c> es
    /// responsable de restaurar <see cref="Inner"/> al terminar el tutorial. No va
    /// a scope Run: ClearScope borraría la key y dejaría al juego sin provider.
    /// </remarks>
    public sealed class TutorialInitiativeProvider : IInitiativeProvider
    {
        private readonly IInitiativeProvider _inner;

        public TutorialInitiativeProvider(IInitiativeProvider inner)
        {
            _inner = inner;
        }

        /// <summary>Provider default decorado — para restaurarlo en el teardown.</summary>
        public IInitiativeProvider Inner => _inner;

        public int RollInitiative(Guid entityGuid)
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var player)
                && player != null && entityGuid == player.PlayerGuid)
            {
                return int.MaxValue;
            }

            int roll = _inner?.RollInitiative(entityGuid) ?? 0;
            return Math.Min(roll, int.MaxValue - 1);
        }
    }
}
