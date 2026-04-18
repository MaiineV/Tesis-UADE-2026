using System;
using Patterns;
using Rollgeon.Balance;

namespace Rollgeon.Combat.Weakness
{
    /// <summary>
    /// Implementacion default de <see cref="IWeaknessChecker"/>. Resuelve weakness consultando
    /// un <see cref="IWeaknessRegistry"/> y aplicando el default del
    /// <see cref="RulesetSO.Weakness"/> o el override per-enemy.
    /// <para>
    /// <b>Side-effect.</b> Cuando el multiplier &gt; 1.0f, dispara
    /// <see cref="EventName.OnWeaknessHit"/> via <see cref="EventManager"/> con
    /// <c>args = [attackerGuid, targetGuid]</c>. Los listeners deben ser informativos /
    /// idempotentes — el efecto real va por <c>TypedEvent&lt;DamageResolvedPayload&gt;</c>
    /// downstream (T100b).
    /// </para>
    /// </summary>
    public sealed class WeaknessChecker : IWeaknessChecker
    {
        private readonly IWeaknessRegistry _registry;
        private readonly RulesetSO _ruleset;

        public WeaknessChecker(IWeaknessRegistry registry, RulesetSO ruleset)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _ruleset = ruleset ?? throw new ArgumentNullException(nameof(ruleset));
        }

        /// <inheritdoc />
        public float GetMultiplier(Guid attacker, Guid target, string matchedComboId)
        {
            if (target == Guid.Empty) return 1.0f;
            if (string.IsNullOrEmpty(matchedComboId)) return 1.0f;

            if (!_registry.TryGet(target, out var data)) return 1.0f;
            if (string.IsNullOrEmpty(data.comboId)) return 1.0f;
            if (data.comboId != matchedComboId) return 1.0f;

            float multiplier = data.mult > 0f
                ? data.mult
                : _ruleset.Weakness.DefaultMultiplier;

            if (multiplier <= 1.0f) return 1.0f;

            // Fire event — informativo, no-transaccional (plan §10.6).
            EventManager.Trigger(EventName.OnWeaknessHit, attacker, target);

            return multiplier;
        }

        /// <summary>
        /// Helper opcional — algunos callers prefieren reportar el hit explicitamente
        /// (sin re-computar el multiplier). Dispara <see cref="EventName.OnWeaknessHit"/>.
        /// </summary>
        public static void ReportHit(Guid attacker, Guid target)
        {
            if (target == Guid.Empty) return;
            EventManager.Trigger(EventName.OnWeaknessHit, attacker, target);
        }
    }
}
