using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Player;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Gate compartido por los HUDs de combate y exploración para el slot de
    /// Healing (BUG-017): con la vida llena el heal no aporta nada, así que el
    /// botón debe quedar deshabilitado. El clamp real del overheal vive en
    /// HealPipeline; esto sólo evita gastar el turno/energía/poción en vano.
    /// </summary>
    public static class HealAvailability
    {
        /// <summary>
        /// True si el player puede recibir curación (HP actual &lt; max HP).
        /// Si los servicios todavía no están registrados (bootstrap incompleto,
        /// tests sin wiring) devuelve true: el gate nunca debe lockear de más.
        /// </summary>
        public static bool CanHealMore(Guid playerGuid)
        {
            if (playerGuid == Guid.Empty) return true;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return true;

            var health = attrs.GetAttribute<Health>(playerGuid);
            if (health == null) return true;

            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps?.CurrentHero == null)
                return true;

            int maxHp = ps.CurrentHero.BaseMaxHp;
            if (maxHp <= 0) return true;

            // Mismo criterio que HealPipeline.Resolve: headroom = max - Value.
            return health.Value < maxHp;
        }
    }
}
