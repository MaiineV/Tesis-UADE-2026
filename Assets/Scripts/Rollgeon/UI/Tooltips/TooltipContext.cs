using System;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Heroes;
using Rollgeon.Phase;
using Rollgeon.Player;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Contexto que un tooltip necesita para insertar datos dinámicos del dueño de la
    /// acción (stats del character, fase). A diferencia del <see cref="EffectContext"/>
    /// de ejecución, existe en hover-time: no hay target ni tirada, solo el owner.
    /// Pensado para expansión de personajes: cualquier entidad futura puede armar su
    /// contexto sin pasar por <see cref="IPlayerService"/>.
    /// </summary>
    public readonly struct TooltipContext
    {
        /// <summary>Dueño de la acción descripta (hoy el player; a futuro cualquier entidad).</summary>
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
        /// <see cref="EffectContext"/> mínimo para ejecutar un <c>EffectIntReader</c> en
        /// hover-time (ej. <c>ReadEntityStat</c> leyendo un atributo del owner). Sin
        /// target: readers que lean del Target devuelven 0.
        /// </summary>
        public EffectContext ToReaderContext()
        {
            return new EffectContext { SourceGuid = OwnerGuid };
        }
    }
}
