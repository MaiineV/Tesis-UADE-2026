using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;

namespace Rollgeon.Player
{
    /// <summary>
    /// Lectura canónica del HP máximo del jugador (BUG-022). Prioridad:
    /// <see cref="MaxHealth"/>.ModifiedValue (base + grants in-run del canal Character);
    /// fallback <c>CurrentHero.BaseMaxHp</c> (bootstrap incompleto / tests sin registro);
    /// 0 si no hay forma de resolver — el caller decide su default permisivo.
    /// </summary>
    public static class PlayerMaxHp
    {
        public static int Resolve(Guid guid)
        {
            if (guid != Guid.Empty
                && ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
            {
                var max = attrs.GetAttribute<MaxHealth>(guid);
                if (max != null && max.ModifiedValue > 0) return max.ModifiedValue;
            }

            if (ServiceLocator.TryGetService<IPlayerService>(out var ps)
                && ps != null && ps.PlayerGuid == guid
                && ps.CurrentHero != null && ps.CurrentHero.BaseMaxHp > 0)
            {
                return ps.CurrentHero.BaseMaxHp;
            }

            return 0;
        }
    }
}
