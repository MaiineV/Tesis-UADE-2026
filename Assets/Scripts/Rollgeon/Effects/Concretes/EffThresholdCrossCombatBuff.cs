using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Items;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Buff de Attack que se LATCHEA al cruzar un umbral de vida (porcentaje del máximo)
    /// y dura hasta el final del combate. Una vez por combate. Pensado para items tipo
    /// "Instinto de Supervivencia" (GDD: +10 de daño al bajar de 30% HP).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es el complemento de <see cref="EffLowHpAttackBuff"/> (la pasiva del Warrior), que
    /// es un *maintainer*: agrega Y remueve según la vida actual. Este es un *latch*:
    /// una vez puesto, curarse NO lo saca — solo el fin de combate, vía
    /// <see cref="ModifierLifetime.Encounter"/>. Mientras el modifier exista, el effect
    /// es no-op: el propio modifier ES el estado "ya disparó este combate", así que no
    /// hay servicio ni contador que resetear.
    /// </para>
    /// <para>
    /// La identidad del modifier sale de <see cref="ItemPassiveSourceId.For"/> sobre
    /// <see cref="EffectContext.SourceItemId"/>: N items pueden usar este mismo effect
    /// sin pisarse, y quitar el item barre su buff junto con sus persistent modifiers
    /// (mismo SourceId). Umbral con la vida YA cruzada también latchea (entrar al combate
    /// bajo el 30% cuenta como estar en peligro — el cruce estricto exigiría trackear el
    /// valor previo por beneficio marginal).
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffThresholdCrossCombatBuff : BaseEffect
    {
        [Title("Umbral")]
        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Fracción del HP máximo: en o por debajo de esto, latchea. GDD Instinto: 0.3.")]
        private float _hpThresholdPercent = 0.3f;

        [Title("Bonus")]
        [SerializeField]
        [Tooltip("Puntos de Attack que se suman (Intrinsic, Add) hasta el fin del combate.")]
        private int _attackBonus = 10;

        public override string GetEffectName() =>
            $"Threshold Cross Combat Buff (HP <= {_hpThresholdPercent:P0} -> +{_attackBonus} Attack hasta fin de combate)";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var target = context.SourceGuid != Guid.Empty ? context.SourceGuid : context.TargetGuid;
            if (target == Guid.Empty) return false;

            var sourceId = ItemPassiveSourceId.For(context.SourceItemId);
            if (sourceId == Guid.Empty)
            {
                Debug.LogWarning("[EffThresholdCrossCombatBuff] Sin SourceItemId en el contexto — " +
                                 "este effect solo funciona en hooks de items (necesita identidad por item).");
                return false;
            }

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning("[EffThresholdCrossCombatBuff] AttributesManager not registered.");
                return false;
            }

            var health = attrs.GetAttribute<Health>(target);
            var attack = attrs.GetAttribute<Attack>(target);
            var maxHealth = attrs.GetAttribute<MaxHealth>(target);
            if (health == null || attack == null || maxHealth == null) return false;

            int max = maxHealth.ModifiedValue;
            if (max <= 0) return false;

            // Latch ya puesto = ya disparó este combate. No-op idempotente.
            foreach (var modifier in attack.GetRawModifiers())
                if (modifier.SourceId == sourceId) return true;

            bool crossed = health.Value > 0 && health.Value <= Mathf.RoundToInt(max * _hpThresholdPercent);
            if (!crossed) return true;

            var buff = new Modifier<int>(_attackBonus, ModifierOperation.Add, duration: 0,
                carrierId: target, sourceId: sourceId,
                dir: ModifierDirection.Intrinsic, lifetime: ModifierLifetime.Encounter,
                tickEvent: default);
            attrs.AddModifier<Attack, int>(target, buff);
            return true;
        }
    }
}
