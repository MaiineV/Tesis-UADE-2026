using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Effect genérico que muta un atributo int del target con una de las 5 operaciones
    /// (Add/Subtract/Multiply/Divide/Set). El stat se elige via <see cref="StatType"/>
    /// (Health/Attack/Speed/Energy/Shield/HealStrength) y el amount viene de Constant,
    /// ComboValue (sistema de combos de dados) o FromReader.
    /// </summary>
    /// <remarks>
    /// <b>Rolls/Reroll budget no soportado.</b> Reroll budget vive en
    /// <c>IRerollBudgetService</c> y no es un <c>BaseAttribute&lt;int&gt;</c>. Si se necesita
    /// modificar rolls vía effect, agregar un effect dedicado en otro PR.
    /// <para>
    /// <b>Target resolution:</b> usa <c>context.TargetGuid</c>; fallback a <c>SourceGuid</c>.
    /// Para auto-target en AI, el designer pone <c>TargetSelector_Self</c> en el
    /// <c>EffectData.TargetSelector</c>. En hero, el UI setea <c>TargetGuid</c> via
    /// SelectionResult.
    /// </para>
    /// <para>
    /// <b>Eventos:</b> <c>AttributesManager.SetAttributeValue</c> dispara <c>OnAttributeChanged</c>
    /// automáticamente. No emitimos eventos stat-específicos (a diferencia de <c>EffAddShield</c>
    /// que dispara <c>OnShieldChanged</c>) — ese ducting se hace en effects dedicados.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffModifyIntAttribute : BaseEffect
    {
        [Title("Target Attribute")]
        public StatType TargetStat = StatType.Energy;

        [Title("Operation")]
        public IntOperation Operation = IntOperation.Add;

        [Title("Amount")]
        [SerializeField]
        [Tooltip("Constant: usa _baseAmount. ComboValue: BaseDamage del combo dice match × _comboMultiplier. " +
                 "FromReader: resuelve via reader × _readerMultiplier.")]
        private DamageSource _amountSource = DamageSource.Constant;

        [SerializeField, ShowIf("_amountSource", DamageSource.Constant)]
        private int _baseAmount = 1;

        [SerializeField, ShowIf("_amountSource", DamageSource.ComboValue)]
        [MinValue(0.01f)]
        [Tooltip("Multiplier aplicado al combo's BaseDamage.")]
        private float _comboMultiplier = 1f;

        [OdinSerialize, SerializeReference]
        [ShowIf("_amountSource", DamageSource.FromReader)]
        [Tooltip("Reader polimórfico que resuelve el valor desde stats de entidad en runtime.")]
        private EffectIntReader _reader;

        [SerializeField, ShowIf("_amountSource", DamageSource.FromReader)]
        [MinValue(0.01f)]
        [Tooltip("Multiplicador aplicado al resultado del reader.")]
        private float _readerMultiplier = 1f;

        public override string GetEffectName() => $"Modify {TargetStat} ({Operation})";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            int amount = ResolveAmount(context);

            Guid target = context.TargetGuid != Guid.Empty ? context.TargetGuid : context.SourceGuid;
            if (target == Guid.Empty)
            {
                Debug.LogWarning("[EffModifyIntAttribute] No target resolved (TargetGuid and SourceGuid both empty) — aborting chain.");
                return false;
            }

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning("[EffModifyIntAttribute] AttributesManager not registered.");
                return false;
            }

            return ApplyToStat(attrs, target, amount);
        }

        private int ResolveAmount(EffectContext context) => _amountSource switch
        {
            DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
                => Mathf.RoundToInt(combo.BaseDamage * _comboMultiplier),
            DamageSource.ComboValue => 0,
            DamageSource.FromReader when _reader != null
                => Mathf.RoundToInt(_reader.Read(context) * _readerMultiplier),
            DamageSource.FromReader => 0,
            _ => _baseAmount,
        };

        private bool ApplyToStat(AttributesManager attrs, Guid target, int amount) => TargetStat switch
        {
            StatType.Health       => Apply<Health>(attrs, target, amount),
            StatType.Attack       => Apply<Attack>(attrs, target, amount),
            StatType.Speed        => Apply<Speed>(attrs, target, amount),
            StatType.Energy       => Apply<Energy>(attrs, target, amount),
            StatType.Shield       => Apply<Shield>(attrs, target, amount),
            StatType.HealStrength => Apply<HealStrength>(attrs, target, amount),
            _                     => true,
        };

        private bool Apply<TAttr>(AttributesManager attrs, Guid target, int amount)
            where TAttr : class, IModifiable<int>
        {
            int current = attrs.GetAttributeValue<TAttr, int>(target);
            int next;
            switch (Operation)
            {
                case IntOperation.Add:      next = current + amount; break;
                case IntOperation.Subtract: next = current - amount; break;
                case IntOperation.Multiply: next = current * amount; break;
                case IntOperation.Divide:
                    if (amount == 0)
                    {
                        Debug.LogWarning($"[EffModifyIntAttribute] Divide-by-zero on {TargetStat}; no-op.");
                        return true;
                    }
                    next = current / amount; break;
                case IntOperation.Set:      next = amount; break;
                default:                    return true;
            }
            attrs.SetAttributeValue<TAttr, int>(target, next);
            return true;
        }
    }
}
