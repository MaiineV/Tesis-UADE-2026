using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Ataque self-contained del Boss Floor Manager. Plan §10.3.
    /// <list type="number">
    /// <item>Consulta la energia del boss — primero via <see cref="EnergyProbe"/> (si el spawner
    /// lo inyecto), si no, cae al stat <c>Energy</c> via <see cref="IEnergyService"/>.</item>
    /// <item>Si la energia esta llena, rodea <see cref="BossFloorManagerSO.DoubleDamageChanceWhenEnergyFull"/>;
    /// si no, rodea <see cref="BossFloorManagerSO.DoubleDamageChanceDefault"/>.</item>
    /// <item>Aplica <see cref="BaseAttackPower"/> (o 2x en caso de hit) al target via
    /// <c>AttributesManager.Modify&lt;Health,int&gt;</c>.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>[STUB] — damage pipeline.</b> Hasta que exista el damage system (§12.2), este
    /// behavior aplica el dano directo sobre <c>Health</c>. Cuando el pipeline mergee, este
    /// archivo migra a <c>EffDealDamage</c> + resolver de doble dano en un reactor.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class BossAttackBehavior : BaseBehavior
    {
        public override string BehaviorName => "Boss Attack";

        [Tooltip("Override opcional del BossFloorManagerSO con los tuning values.")]
        public BossFloorManagerSO BossDataOverride;

        [MinValue(0)]
        [Tooltip("Dano base del ataque (pre-double-damage). En runtime real lo reemplaza AttackPower del stat.")]
        public int BaseAttackPower = 12;

        /// <summary>
        /// Funcion rng que retorna un float en [0,1). Default <see cref="UnityEngine.Random.value"/>.
        /// Inyectable en tests para determinismo.
        /// </summary>
        [NonSerialized]
        public Func<float> RandomSource;

        /// <summary>
        /// Resolver opcional de la energia del boss. Si null, el behavior pregunta al
        /// <see cref="IEnergyService"/> por el stat <c>Energy</c> del owner. El spawner del Boss
        /// suele asignar <c>() =&gt; energyBehavior.CurrentEnergy</c> para leer la energia
        /// self-contained del <see cref="BossEnergyBuildupBehavior"/>.
        /// </summary>
        [NonSerialized]
        public Func<int> EnergyProbe;

        /// <summary>
        /// Resolver del max-energy del boss. Se pide al SO si BossDataOverride != null;
        /// si no, fallback a 0 (NO full).
        /// </summary>
        [NonSerialized]
        public Func<int> EnergyMaxProbe;

        /// <summary>Target del ataque. En runtime lo resuelve el AIRoot; aqui lo inyecta el caller / test.</summary>
        [NonSerialized]
        public Guid TargetGuid;

        /// <inheritdoc />
        public override void Execute(BehaviorContext ctx)
        {
            if (ctx == null || ctx.SourceEntity == null) return;
            if (TargetGuid == Guid.Empty) return;

            var so = BossDataOverride;
            if (so == null)
            {
                Debug.LogWarning(
                    "[BossAttackBehavior] BossFloorManagerSO no asignado. " +
                    "Asigna BossDataOverride en el Inspector.");
                return;
            }

            // [STUB] — damage pipeline.
            int damage = BaseAttackPower;

            float chance = IsEnergyFull(ctx)
                ? so.DoubleDamageChanceWhenEnergyFull
                : so.DoubleDamageChanceDefault;

            float roll = RandomSource != null ? RandomSource() : UnityEngine.Random.value;
            if (roll < chance)
            {
                damage *= 2;
            }

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning("[BossAttackBehavior] AttributesManager no registrado; skip damage.");
                return;
            }

            int dmg = damage;
            attrs.Modify<Health, int>(TargetGuid, current =>
            {
                int next = current - dmg;
                return next < 0 ? 0 : next;
            });
        }

        private bool IsEnergyFull(BehaviorContext ctx)
        {
            int current;
            int max;

            if (EnergyProbe != null)
            {
                current = EnergyProbe();
                max = EnergyMaxProbe != null
                    ? EnergyMaxProbe()
                    : (BossDataOverride != null ? BossDataOverride.BossEnergyMax : 0);
            }
            else
            {
                if (!ServiceLocator.TryGetService<IEnergyService>(out var energy) || energy == null)
                {
                    return false;
                }
                var ownerGuid = ctx.SourceEntity.Guid;
                current = energy.GetCurrent(ownerGuid);
                max = energy.GetMax(ownerGuid);
            }

            return max > 0 && current >= max;
        }
    }
}
