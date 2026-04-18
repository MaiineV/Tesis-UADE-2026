using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Stubs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Behavior del Support (arquetipo "Auditor de Mesa" — TECHNICAL.md §7.1 / Content#0099).
    /// En su turno elige al aliado con HP absoluto mas bajo (filtrando heridos) y le aplica
    /// un heal cuyo monto = <see cref="BaseHealAmount"/> + <see cref="HealStrength"/> del owner.
    /// Si no hay aliados heridos, no-op (idle).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Seleccion del target (AI decision).</b>
    /// <list type="number">
    /// <item>Pide aliados via <see cref="IEntityQueryService.GetAllAlliesOf"/>.</item>
    /// <item>Filtra vivos (<c>Health.Value &gt; 0</c>) y heridos (<c>Health.Value &lt; MaxHP</c>).</item>
    /// <item>Argmin por <c>Health.Value</c> absoluto; empate → menor <see cref="Guid.CompareTo"/>.</item>
    /// <item>Si no hay candidatos, <see cref="Execute"/> retorna sin efecto.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Aplicacion del heal.</b> Via <c>AttributesManager.Modify&lt;Health,int&gt;</c>.
    /// El clamp contra <c>MaxHP</c> se hace dentro del mutator (el <see cref="Health"/>
    /// atributo no clampea por si mismo, plan §10.5). Al mutar, el <c>AttributesManager</c>
    /// dispara <c>OnAttributeChanged</c> para que el HUD se entere.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class SupportHealBehavior : BaseBehavior
    {
        [Title("Heal Config")]
        [MinValue(1)]
        [Range(1, 50)]
        [Tooltip("Curacion base antes del bonus de HealStrength del owner.")]
        public int BaseHealAmount = 6;

        public override string BehaviorName => "Support — Heal Lowest-HP Ally";

        /// <summary>
        /// Delegate opcional para resolver el HP maximo de un aliado. Si <c>null</c>, el
        /// clamp cae al valor actual + heal (sin cap). En produccion lo inyecta el spawner
        /// (T100d+) con acceso al <see cref="EnemyDataSO.BaseHP"/> de cada entity.
        /// </summary>
        [NonSerialized]
        public Func<Guid, int> MaxHpResolver;

        /// <inheritdoc />
        public override void Execute(BehaviorContext ctx)
        {
            if (ctx == null || ctx.SourceEntity == null) return;

            var ownerGuid = ctx.SourceEntity.Guid;
            var target = PickLowestHpWoundedAlly(ownerGuid);
            if (target == null) return; // idle — no wounded allies.

            int healAmount = ResolveHealAmount(ownerGuid);
            if (healAmount <= 0) return;

            ApplyHeal(target.Guid, healAmount);
            RegisterFloatingHeal(target.Guid, healAmount);
        }

        // ----- target selection ------------------------------------------

        /// <summary>Expone la heuristica de seleccion para tests sin construir un BehaviorContext.</summary>
        public Entity PickLowestHpWoundedAlly(Guid ownerGuid)
        {
            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var service) || service == null)
            {
                Debug.LogWarning(
                    "[SupportHealBehavior] IEntityQueryService not registered — cannot resolve allies. " +
                    "Register the service in bootstrap (TECHNICAL.md §1.1).");
                return null;
            }

            var allies = service.GetAllAlliesOf(ownerGuid);
            if (allies == null) return null;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning(
                    "[SupportHealBehavior] AttributesManager not registered — cannot read ally HP.");
                return null;
            }

            Entity best = null;
            int bestHp = int.MaxValue;
            foreach (var ally in allies)
            {
                if (ally == null) continue;
                if (ally.Guid == ownerGuid) continue; // defensive — ownerGuid never as ally.
                var health = attrs.GetAttribute<Health>(ally.Guid);
                if (health == null) continue;
                int hp = health.Value;
                if (hp <= 0) continue; // dead → skip.
                int max = MaxHpResolver != null ? MaxHpResolver(ally.Guid) : int.MaxValue;
                if (hp >= max) continue; // full HP → skip.

                if (hp < bestHp ||
                    (hp == bestHp && best != null && ally.Guid.CompareTo(best.Guid) < 0))
                {
                    best = ally;
                    bestHp = hp;
                }
            }
            return best;
        }

        // ----- heal amount -----------------------------------------------

        private int ResolveHealAmount(Guid ownerGuid)
        {
            int bonus = 0;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
            {
                var hs = attrs.GetAttribute<HealStrength>(ownerGuid);
                if (hs != null) bonus = hs.ModifiedValue;
            }
            return BaseHealAmount + bonus;
        }

        // ----- apply heal ------------------------------------------------

        private void ApplyHeal(Guid targetGuid, int amount)
        {
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return;

            int maxHp = MaxHpResolver != null ? MaxHpResolver(targetGuid) : int.MaxValue;
            attrs.Modify<Health, int>(targetGuid, current =>
            {
                int healed = current + amount;
                return healed > maxHp ? maxHp : healed;
            });
        }

        private void RegisterFloatingHeal(Guid targetGuid, int amount)
        {
            SetBehaviorValue(
                BehaviorValueKey.FloatingHeal,
                new FloatingNumberBehaviorValue
                {
                    Value = amount,
                    TargetEntityGuid = targetGuid,
                });
        }
    }
}
