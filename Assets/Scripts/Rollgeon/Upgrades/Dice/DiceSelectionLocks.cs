using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Effects;
using Rollgeon.Player;
using Rollgeon.PreConditions;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Gate de selección por encantamiento (<see cref="CapSelectionRequirement"/>): un dado
    /// cuyo requisito no se cumple no se puede seleccionar ni entra al combo. Es el espejo
    /// del bloqueo del Boss 1 (<c>IDiceBlockService</c>) pero derivado de estado del jugador
    /// (oro, vida) en vez de un set por turno — por eso se evalúa en cada consulta y no se
    /// guarda. El bloqueo del boss lo lee la IA; este no, y no deben mezclarse.
    /// </summary>
    public static class DiceSelectionLocks
    {
        private const string LockSuffix = ".lock";

        /// <summary>
        /// <c>true</c> si algún encantamiento del dado en <paramref name="bagSlot"/> del
        /// jugador tiene un requisito de selección que hoy no se cumple.
        /// <paramref name="label"/> es el texto localizado del candado (null si libre).
        /// </summary>
        public static bool IsPlayerSlotLocked(int bagSlot, out string label)
        {
            label = null;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var ench) || ench?.Bag == null)
                return false;
            var owner = ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;
            return IsSlotLocked(ench.Bag, bagSlot, owner, out label);
        }

        /// <summary>
        /// Forma pura sobre un bag: evalúa las <see cref="CapSelectionRequirement"/> del slot
        /// con el contexto de <paramref name="owner"/>. Null-safe y tolerante a índices
        /// fuera de rango (⇒ libre).
        /// </summary>
        public static bool IsSlotLocked(RuntimeDiceBag bag, int bagSlot, Guid owner, out string label)
        {
            label = null;
            if (bag == null || bagSlot < 0 || bagSlot >= bag.Dice.Count) return false;
            var slots = bag.GetEnchantments(bagSlot);
            if (slots == null) return false;

            PreConditionContext ctx = null;
            for (int i = 0; i < slots.Count; i++)
            {
                var ench = slots[i];
                var caps = ench?.Capabilities;
                if (caps == null) continue;
                for (int c = 0; c < caps.Count; c++)
                {
                    if (!(caps[c] is CapSelectionRequirement req)) continue;
                    ctx ??= EnchantmentPreConditionContexts.ForOwner(owner, Guid.Empty, effect: null);
                    if (Satisfied(req, ctx)) continue;
                    label = Rollgeon.Localization.LocalizedContent.Resolve(
                        ench.UpgradeId, LockSuffix, req.LockLabel);
                    return true;
                }
            }
            return false;
        }

        private static bool Satisfied(CapSelectionRequirement req, PreConditionContext ctx)
        {
            var conditions = req.Conditions;
            if (conditions == null) return true;
            for (int i = 0; i < conditions.Count; i++)
            {
                var pc = conditions[i];
                if (pc != null && !pc.Evaluate(ctx)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Arma el <see cref="PreConditionContext"/> del dueño del bag para el canal dados. Las
    /// PCs genéricas de stats lo necesitan completo: <c>PcOwnerStatCompare</c> devuelve
    /// <c>true</c> sin <c>Attributes</c> y <c>PcOwnerHpBelow</c> <c>false</c> sin
    /// <c>OwnerMaxHp</c> — sin esto Vampiro no podría evitar matar al jugador.
    /// </summary>
    public static class EnchantmentPreConditionContexts
    {
        public static PreConditionContext ForOwner(Guid owner, Guid opponent, EffectContext effect)
        {
            AttributesManager attrs = null;
            ServiceLocator.TryGetService(out attrs);
            return new PreConditionContext
            {
                OwnerGuid = owner,
                OpponentGuid = opponent,
                Effect = effect,
                Attributes = attrs,
                OwnerMaxHp = Rollgeon.Combat.MaxHpResolver.TryResolve(owner, out int maxHp) ? maxHp : (int?)null,
            };
        }
    }
}
