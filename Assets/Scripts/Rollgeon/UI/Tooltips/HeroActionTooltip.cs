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
    /// Texto de tooltip de una acción de hero: nombre + costo + body de los effects.
    /// Nunca vacío: sin body queda header + costo.
    /// </summary>
    public static class HeroActionTooltip
    {
        // Por Slot y no por ActionName: retipear el nombre visible no rompe la key. Solo
        // confiable con IsBaseBehavior true.
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

            sb.AppendLine().Append(
                LocalizedContent.Ui("tooltip.hero_action.cost_per_roll", "Costo: 1 Roll por tirada"));

            var body = FirstEffectTooltip(behavior.Effects, context);
            if (!string.IsNullOrEmpty(body))
                sb.AppendLine().Append(body);

            return sb.ToString();
        }

        private static string ResolveActionName(HeroActionBehavior behavior)
        {
            var key = ResolveActionNameKey(behavior);
            return key != null ? LocalizedContent.Ui(key, behavior.ActionName) : behavior.ActionName;
        }

        /// <summary>
        /// Key de la tabla UI del nombre, o <c>null</c> sin mapeo confiable. Separado para
        /// testear el mapeo sin Localization inicializada.
        /// </summary>
        internal static string ResolveActionNameKey(HeroActionBehavior behavior)
        {
            if (behavior == null) return null;

            if (behavior.IsBaseBehavior && BaseSlotKeys.TryGetValue(behavior.Slot, out var key))
                return key;

            // "Pass door" comparte slot con "Force Door" (IsBaseBehavior false): va por nombre.
            if (string.Equals(behavior.ActionName, "Pass door", StringComparison.OrdinalIgnoreCase))
                return "action.pass_door";

            return null;
        }

        /// <summary>
        /// Primer texto no-vacío de los effects, recursando en las fases de <see cref="EffChain"/>.
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
            // Los compuestos concatenan lo que anidan; EffectTree sabe cuáles anidan.
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
