using System;
using Rollgeon.Phase;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Mascara de fases del juego en las que un <see cref="BaseBehavior"/> puede ejecutar.
    /// TECHNICAL.md §7.2. Usa los valores del enum <see cref="GamePhase"/> (stub de F#0004)
    /// elevados a bits.
    /// </summary>
    /// <remarks>
    /// <b>Regla de estabilidad</b>: los valores de bit NO se re-asignan — siempre se suman
    /// al final. <c>All</c> se computa como OR de todos los bits validos.
    /// </remarks>
    [Flags]
    public enum GamePhaseMask
    {
        None = 0,
        Exploration = 1 << 1,
        Combat = 1 << 2,
        All = ~0,
    }

    /// <summary>
    /// Helpers para testear si una <see cref="GamePhase"/> esta contenida en un
    /// <see cref="GamePhaseMask"/>.
    /// </summary>
    public static class GamePhaseMaskExtensions
    {
        public static bool Allows(this GamePhaseMask mask, GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Exploration: return (mask & GamePhaseMask.Exploration) != 0;
                case GamePhase.Combat: return (mask & GamePhaseMask.Combat) != 0;
                case GamePhase.None: return mask == GamePhaseMask.None;
                case GamePhase.Loading:
                case GamePhase.GameOver:
                default:
                    return false;
            }
        }
    }
}
