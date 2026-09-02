using System;
using UnityEngine;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Auto-resuelve el <see cref="IHasTooltipInfo"/> de un trigger (GO, padres, hijos)
    /// sin requerir un binder.
    /// </summary>
    public static class TooltipResolver
    {
        /// <summary>Null si no hay <see cref="IHasTooltipInfo"/> en la jerarquía.</summary>
        public static Func<string> AutoResolve(Component trigger)
        {
            if (trigger == null) return null;

            var info = trigger.GetComponentInParent<IHasTooltipInfo>();
            if (info == null)
            {
                info = trigger.GetComponentInChildren<IHasTooltipInfo>(includeInactive: true);
            }

            if (info == null) return null;
            return info.BuildTooltip;
        }
    }
}
