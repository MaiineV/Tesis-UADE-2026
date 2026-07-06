using System;
using System.Collections.Generic;
using System.Text;
using Rollgeon.ActionRolls;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Builder compartido del texto de tooltip de una acción de hero: header con el
    /// nombre + costo real de energía + body aportado por los effects
    /// (<see cref="IHasTooltipInfo"/>). Nunca devuelve vacío: sin body queda el
    /// header + costo como fallback genérico.
    /// </summary>
    public static class HeroActionTooltip
    {
        public static string BuildFor(HeroActionBehavior behavior, in TooltipContext context)
        {
            if (behavior == null) return null;

            var sb = new StringBuilder();
            sb.Append("<b>").Append(behavior.ActionName).Append("</b>");

            int cost = ResolveDisplayCost(behavior, context.OwnerGuid);
            if (cost > 0)
                sb.AppendLine().Append("Costo: ").Append(cost).Append(" de energía");

            var body = FirstEffectTooltip(behavior.Effects, context);
            if (!string.IsNullOrEmpty(body))
                sb.AppendLine().Append(body);

            return sb.ToString();
        }

        /// <summary>
        /// Primer texto no-vacío aportado por los effects. Recursa dentro de
        /// <see cref="EffChain"/> concatenando las fases (los ataques del guerrero
        /// envuelven daño + escudo en fases de chain — sin recursión no habría texto).
        /// </summary>
        public static string FirstEffectTooltip(List<EffectData> effects, in TooltipContext context)
        {
            if (effects == null) return null;
            foreach (var group in effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    var text = TooltipFrom(eff, context);
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            return null;
        }

        /// <summary>
        /// Costo que efectivamente se va a cobrar: el <c>ActionRollSpec</c> del effect
        /// (cobrado por <c>IActionRollService</c>) pisa al <c>behavior.EnergyCost</c>
        /// legacy — misma regla que el cost label de los botones del HUD.
        /// </summary>
        public static int ResolveDisplayCost(HeroActionBehavior behavior, Guid ownerGuid)
        {
            if (behavior?.Effects != null)
            {
                foreach (var group in behavior.Effects)
                {
                    if (group?.Effects == null) continue;
                    foreach (var eff in group.Effects)
                    {
                        if (eff is IActionRollEffect rollEffect
                            && rollEffect.TryGetRollSpec(ownerGuid, out var spec))
                            return spec.EnergyCost;
                    }
                }
            }
            return behavior?.EnergyCost ?? 0;
        }

        private static string TooltipFrom(IEffect eff, in TooltipContext context)
        {
            if (eff is EffChain chain && chain.Phases != null)
            {
                var sb = new StringBuilder();
                foreach (var phase in chain.Phases)
                {
                    var inner = phase?.Effects?.Effects;
                    if (inner == null) continue;
                    foreach (var innerEff in inner)
                    {
                        var text = TooltipFrom(innerEff, context);
                        if (string.IsNullOrEmpty(text)) continue;
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(text);
                    }
                }
                return sb.Length > 0 ? sb.ToString() : null;
            }

            return eff is IHasTooltipInfo info ? info.BuildTooltip(context) : null;
        }
    }
}
