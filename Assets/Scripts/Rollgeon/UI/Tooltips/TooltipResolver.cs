using System;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Helper compartido por los triggers (UI y World) para auto-resolver el
    /// <see cref="IHasTooltipInfo"/> sin requerir un binder específico.
    /// </summary>
    /// <remarks>
    /// Estrategia: busca un <see cref="MonoBehaviour"/> que implemente
    /// <see cref="IHasTooltipInfo"/> en este GameObject, después en sus padres,
    /// y por último en sus hijos (incluyendo inactivos). Si encuentra uno,
    /// devuelve un provider que llama <c>BuildTooltip()</c> cada vez.
    /// </remarks>
    public static class TooltipResolver
    {
        /// <summary>
        /// Intenta resolver un provider para <paramref name="trigger"/>. Devuelve
        /// <c>null</c> si no hay <see cref="IHasTooltipInfo"/> en la jerarquía —
        /// en ese caso el trigger queda sin texto (no-op silencioso).
        /// </summary>
        public static Func<string> AutoResolve(Component trigger)
        {
            if (trigger == null) return null;

            // Mismo GO + padres.
            var info = trigger.GetComponentInParent<IHasTooltipInfo>();
            if (info == null)
            {
                // Hijos (incluso inactivos).
                info = trigger.GetComponentInChildren<IHasTooltipInfo>(includeInactive: true);
            }

            if (info == null) return null;
            return info.BuildTooltip;
        }
    }
}
