using System;
using System.Collections.Generic;
using System.Text;
using Rollgeon.ActionRolls;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.Localization;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Builder compartido del texto de tooltip de una acción de hero: header con el
    /// nombre + costo en rolls + body aportado por los effects
    /// (<see cref="IHasTooltipInfo"/>). Nunca devuelve vacío: sin body queda el
    /// header + costo como fallback genérico.
    /// </summary>
    public static class HeroActionTooltip
    {
        // Mapeo por Slot y no por ActionName: un diseñador puede retipear el nombre visible
        // sin romper la key. Solo es confiable cuando IsBaseBehavior es true — sin eso, Slot
        // puede quedar en su default sin significado.
        private static readonly Dictionary<HeroBehaviorSlot, string> BaseSlotKeys =
            new Dictionary<HeroBehaviorSlot, string>
            {
                { HeroBehaviorSlot.Movement, "action.move" },
                { HeroBehaviorSlot.BaseAttack, "action.attack" },
                { HeroBehaviorSlot.ClassSkill, "action.class_skill" },
                { HeroBehaviorSlot.Healing, "action.heal" },
                { HeroBehaviorSlot.ForceDoor, "action.force_door" },
                { HeroBehaviorSlot.Defense, "action.defense" },
            };

        public static string BuildFor(HeroActionBehavior behavior, in TooltipContext context)
        {
            if (behavior == null) return null;

            var sb = new StringBuilder();
            sb.Append("<b>").Append(ResolveActionName(behavior)).Append("</b>");

            // Pool de Rolls: toda acción cuesta 1 roll por tirada — costo uniforme.
            sb.AppendLine().Append(
                LocalizedContent.Ui("tooltip.hero_action.cost_per_roll", "Costo: 1 Roll por tirada"));

            var body = FirstEffectTooltip(behavior.Effects, context);
            if (!string.IsNullOrEmpty(body))
                sb.AppendLine().Append(body);

            return sb.ToString();
        }

        /// <summary>Nombre localizado de la acción, resuelto vía <see cref="ResolveActionNameKey"/>.</summary>
        private static string ResolveActionName(HeroActionBehavior behavior)
        {
            var key = ResolveActionNameKey(behavior);
            return key != null ? LocalizedContent.Ui(key, behavior.ActionName) : behavior.ActionName;
        }

        /// <summary>
        /// Key de la tabla UI para el nombre, o <c>null</c> sin mapeo confiable (el caller cae
        /// al <see cref="HeroActionBehavior.ActionName"/> crudo). Separado de
        /// <see cref="ResolveActionName"/> para testear el mapeo sin Localization inicializada.
        /// "Pass door" matchea por ActionName: comparte slot con "Force Door" como variante
        /// con <c>IsBaseBehavior = false</c>, y el mapeo por Slot no las distingue.
        /// </summary>
        internal static string ResolveActionNameKey(HeroActionBehavior behavior)
        {
            if (behavior == null) return null;

            if (behavior.IsBaseBehavior && BaseSlotKeys.TryGetValue(behavior.Slot, out var key))
                return key;

            if (string.Equals(behavior.ActionName, "Pass door", StringComparison.OrdinalIgnoreCase))
                return "action.pass_door";

            return null;
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

        private static string TooltipFrom(IEffect eff, in TooltipContext context)
        {
            // Los efectos compuestos (chain con fases, secuencia con steps InlineEffect)
            // concatenan el texto de lo que anidan. EffectTree es quien sabe cuáles anidan
            // — acá solo se recorre.
            var children = EffectTree.DirectChildren(eff);
            if (children.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var child in children)
                {
                    var text = TooltipFrom(child, context);
                    if (string.IsNullOrEmpty(text)) continue;
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(text);
                }
                return sb.Length > 0 ? sb.ToString() : null;
            }

            return eff is IHasTooltipInfo info ? info.BuildTooltip(context) : null;
        }
    }
}
