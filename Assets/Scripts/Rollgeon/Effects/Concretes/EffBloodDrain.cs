using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Readers;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Blood;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Bandas mixta (4-7, 50%) y positiva (8-10, 100%) de Blood Transfusion (Feature#0084):
    /// el enemigo elegible con mayor HP actual recibe <c>max(1, floor(A × cara / 10))</c> de
    /// daño y el jugador se cura por <see cref="HealPct"/> del daño REAL (HP efectivamente
    /// perdido — <c>DamageContext.FinalDamage</c>, sin contar lo absorbido por escudo).
    /// </summary>
    /// <remarks>
    /// <c>A</c> = cara máxima del dado más grande de la bolsa (<see cref="ReadBiggestBagDieMaxFace"/>).
    /// Sin <see cref="ActiveItemRollTriggerContext"/> o sin enemigo elegible: no-op (nunca
    /// corta la cadena — el roll ya se pagó).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffBloodDrain : BaseEffect
    {
        [Title("Blood Drain")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fracción del daño REAL infligido que el jugador recupera. Mixta: 0.5. Positiva: 1.0.")]
        private float _healPct = 0.5f;

        public float HealPct => _healPct;

        /// <summary>Setter de autoría/tests.</summary>
        public void EditorSetHealPct(float pct) => _healPct = pct;

        public override string GetEffectName() => "Blood Drain";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            if (!ActiveItemRollTriggerContext.TryGet(context, out var rc))
            {
                Debug.LogWarning("[EffBloodDrain] Sin ActiveItemRollTriggerContext — no-op.");
                return true;
            }

            var target = BloodTransfusionTargeting.ResolveDrainTarget(context.SourceGuid);
            if (target == Guid.Empty)
            {
                Debug.Log("[EffBloodDrain] Sin enemigo elegible — no-op.");
                return true;
            }

            int a = new ReadBiggestBagDieMaxFace().Read(context);
            int dmg = Math.Max(1, (int)Math.Floor(a * rc.Face / 10f));

            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var damagePipeline) || damagePipeline == null)
            {
                Debug.LogWarning("[EffBloodDrain] IDamagePipeline no registrado — no se aplica daño ni curación.");
                return true;
            }

            var dmgCtx = new DamageContext
            {
                SourceId = context.SourceGuid,
                TargetId = target,
                BaseDamage = dmg,
                Kind = AttackKind.ScriptedAbility,
            };
            damagePipeline.Resolve(dmgCtx);

            // "Daño real" = HP efectivamente perdido, NUNCA lo absorbido por escudo.
            int realHpLost = dmgCtx.FinalDamage;
            int healAmount = (int)Math.Floor(_healPct * realHpLost);
            if (healAmount <= 0) return true;

            if (!ServiceLocator.TryGetService<IHealPipeline>(out var healPipeline) || healPipeline == null)
            {
                Debug.LogWarning("[EffBloodDrain] IHealPipeline no registrado — no se cura al jugador.");
                return true;
            }

            healPipeline.Resolve(new HealContext
            {
                SourceId = context.SourceGuid,
                TargetId = context.SourceGuid,
                BaseHeal = healAmount,
            });

            return true;
        }
    }
}
