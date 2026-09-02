using System;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Datos dinámicos del dueño de la acción en hover-time: no hay target ni tirada,
    /// solo el owner.
    /// </summary>
    public readonly struct TooltipContext
    {
        /// <summary>Dueño de la acción descripta.</summary>
        public readonly Guid OwnerGuid;

        /// <summary>Data del hero del owner. Puede ser null — los consumers deben tolerar.</summary>
        public readonly ClassHeroSO Hero;

        public readonly GamePhase Phase;

        public TooltipContext(Guid ownerGuid, ClassHeroSO hero, GamePhase phase)
        {
            OwnerGuid = ownerGuid;
            Hero = hero;
            Phase = phase;
        }

        /// <summary>Arma el contexto para el hero actual del <see cref="IPlayerService"/>.</summary>
        public static bool TryForCurrentHero(GamePhase phase, out TooltipContext context)
        {
            if (ServiceLocator.TryGetService<IPlayerService>(out var playerService)
                && playerService?.CurrentHero != null)
            {
                context = new TooltipContext(playerService.PlayerGuid, playerService.CurrentHero, phase);
                return true;
            }

            context = default;
            return false;
        }

        /// <summary>
        /// <see cref="EffectContext"/> mínimo para readers en hover-time. Sin target: los
        /// que lean del Target devuelven 0.
        /// </summary>
        public EffectContext ToReaderContext()
        {
            return new EffectContext { SourceGuid = OwnerGuid };
        }
    }
}
