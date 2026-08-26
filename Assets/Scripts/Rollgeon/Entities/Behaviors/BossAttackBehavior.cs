using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    [Serializable, HideReferenceObjectPicker]
    public class BossAttackBehavior : BaseBehavior
    {
        public override string BehaviorName => "Boss Attack";

        [Tooltip("Override opcional del BossFloorManagerSO con los tuning values.")]
        public BossFloorManagerSO BossDataOverride;

        [MinValue(0)]
        [Tooltip("Dano base del ataque (pre-double-damage). En runtime real lo reemplaza AttackPower del stat.")]
        public int BaseAttackPower = 12;

        /// <summary>Retorna un float en [0,1). Default <see cref="UnityEngine.Random.value"/>; inyectable en tests para determinismo.</summary>
        [NonSerialized]
        public Func<float> RandomSource;

        /// <summary>
        /// Si null, el behavior lee el stat <c>Energy</c> propio del owner. El spawner suele asignar
        /// <c>() =&gt; energyBehavior.CurrentEnergy</c> para leer la del buildup behavior.
        /// </summary>
        [NonSerialized]
        public Func<int> EnergyProbe;

        /// <summary>Se pide al SO si BossDataOverride != null; si no, fallback a 0 (NO full).</summary>
        [NonSerialized]
        public Func<int> EnergyMaxProbe;

        /// <summary>En runtime lo resuelve el AIRoot; aqui lo inyecta el caller / test.</summary>
        [NonSerialized]
        public Guid TargetGuid;

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

            // Escribe Health directo en vez de pasar por DamagePipeline como los nodos de jefe: acá
            // el golpe no cobra debilidad ni armadura.
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
                // El stat Energy propio del boss: presupuesto de IA enemigo, NO el pool de rolls del jugador.
                if (!ServiceLocator.TryGetService<AttributesManager>(out var attrsMgr) || attrsMgr == null)
                    return false;

                var ownerGuid = ctx.SourceEntity.Guid;
                if (!attrsMgr.IsRegistered(ownerGuid)) return false;
                var ownerAttrs = attrsMgr.GetAttributes(ownerGuid);
                if (ownerAttrs == null || !ownerAttrs.HasAttribute<Energy>()) return false;

                current = attrsMgr.GetAttributeValue<Energy, int>(ownerGuid);
                max = BossDataOverride != null ? BossDataOverride.BossEnergyMax : 0;
            }

            return max > 0 && current >= max;
        }
    }
}
