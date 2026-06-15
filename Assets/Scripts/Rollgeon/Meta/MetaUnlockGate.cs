using Patterns;

namespace Rollgeon.Meta
{
    /// <summary>
    /// Fachada estática para que pools y screens consulten disponibilidad sin
    /// acoplarse al lifecycle del servicio (#164). Si el
    /// <see cref="IMetaProgressionService"/> no está registrado (tests, escenas
    /// sin bootstrap), degrada a "todo disponible" — mismo patrón defensivo que
    /// <c>ContractSheet.MatchBest</c> con <c>IComboBlockService</c>.
    /// </summary>
    public static class MetaUnlockGate
    {
        /// <summary><c>true</c> si el elemento puede ofrecerse al jugador.</summary>
        public static bool IsAvailable(UnlockableCategory category, string targetId)
        {
            if (!ServiceLocator.TryGetService<IMetaProgressionService>(out var service) || service == null)
            {
                return true;
            }
            return service.IsAvailable(category, targetId);
        }
    }
}
