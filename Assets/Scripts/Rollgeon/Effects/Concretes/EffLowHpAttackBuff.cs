using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Pasiva del Warrior (spec Santi 2026-07-13): mientras <c>Health.Value</c> del target
    /// esté en <c>1..HpThreshold</c>, mantiene un modifier <c>Add</c> Intrinsic Permanente
    /// de <c>+AttackBonus</c> sobre <see cref="Attack"/>; lo retira apenas la vida sube por
    /// encima del umbral. Reemplaza la pasiva de heal-on-turn (nunca implementada — el
    /// hook estaba vacío).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Idempotente y stateless.</b> No guarda "¿ya está aplicado?" en un campo propio —
    /// cada llamada relee el stack de modifiers de <see cref="Attack"/> buscando uno con
    /// <see cref="PassiveSourceId"/> y decide add/remove/no-op en base al estado real. Esto
    /// lo hace seguro de invocar de más (incluso re-entrante, si <c>OnAttributeChanged</c> se
    /// dispara por nuestro propio <c>AddModifier</c>/<c>RemoveModifier</c>: la segunda
    /// invocación ve el estado ya consistente y no hace nada).
    /// </para>
    /// <para>
    /// <b>Reset entre runs gratis.</b> <c>PassiveSourceId</c> es un GUID constante propio del
    /// effect (no el guid del target) para no colisionar con otros modifiers self-sourced.
    /// El modifier vive en la instancia runtime de <see cref="Attack"/>, que <c>RunController</c>
    /// recrea sin modifiers en cada <c>OnRunStart</c> — no hace falta lógica de "on death".
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffLowHpAttackBuff : BaseEffect
    {
        // GUID fijo de este effect — nunca el guid del target, para no pisar/confundirse con
        // otros modifiers self-sourced sobre Attack. Publico: lo usa PassiveBadgeView (UI) para
        // saber si el buff activo es el nuestro, sin duplicar el criterio de busqueda.
        public static readonly Guid PassiveSourceId = new Guid("8f2b1a3c-7d4e-4f5a-9c6b-1e2d3f4a5b6c");

        [Title("Umbral")]
        [SerializeField, MinValue(1)]
        [Tooltip("HP en o por debajo de este valor activa el buff. GD: 3 (HP 1-3).")]
        private int _hpThreshold = 3;

        [Title("Bonus")]
        [SerializeField]
        [Tooltip("Puntos de Attack que se suman (Intrinsic, Add) mientras el HP esté en el umbral. GD: 5.")]
        private int _attackBonus = 5;

        public override string GetEffectName() =>
            $"Low HP Attack Buff (HP <= {_hpThreshold} -> +{_attackBonus} Attack)";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            Guid target = context.TargetGuid != Guid.Empty ? context.TargetGuid : context.SourceGuid;
            if (target == Guid.Empty) return false;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning("[EffLowHpAttackBuff] AttributesManager not registered.");
                return false;
            }

            var health = attrs.GetAttribute<Health>(target);
            var attack = attrs.GetAttribute<Attack>(target);
            if (health == null || attack == null) return false;

            bool shouldBuff = health.Value > 0 && health.Value <= _hpThreshold;
            Guid existingModifierId = FindOwnModifierId(attack);
            bool isBuffed = existingModifierId != Guid.Empty;

            if (shouldBuff && !isBuffed)
            {
                var modifier = new Modifier<int>(_attackBonus, ModifierOperation.Add, duration: 0,
                    carrierId: target, sourceId: PassiveSourceId,
                    dir: ModifierDirection.Intrinsic, lifetime: ModifierLifetime.Permanent,
                    tickEvent: EventName.OnTurnFinished);
                attrs.AddModifier<Attack, int>(target, modifier);
            }
            else if (!shouldBuff && isBuffed)
            {
                attrs.RemoveModifier<Attack, int>(target, existingModifierId);
            }

            return true;
        }

        /// <summary>
        /// True si <paramref name="entityId"/> tiene ahora mismo el modifier de esta pasiva
        /// puesto sobre <see cref="Attack"/>. Usado por <c>PassiveBadgeView</c> (UI) para decidir
        /// si mostrar el badge — no requiere estado propio, relee el stack de modifiers real.
        /// </summary>
        public static bool IsActiveFor(AttributesManager attrs, Guid entityId)
        {
            var attack = attrs?.GetAttribute<Attack>(entityId);
            if (attack == null) return false;
            return FindOwnModifierId(attack) != Guid.Empty;
        }

        private static Guid FindOwnModifierId(Attack attack)
        {
            foreach (var modifier in attack.GetRawModifiers())
            {
                if (modifier.SourceId == PassiveSourceId) return modifier.ModifierId;
            }
            return Guid.Empty;
        }
    }
}
