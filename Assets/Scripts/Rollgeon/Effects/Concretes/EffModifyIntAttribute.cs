using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Player;
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
    /// automáticamente. Para <see cref="StatType.Health"/> además emitimos el payload resuelto
    /// (<c>HealResolvedPayload</c> si sube, <c>DamageResolvedPayload</c> si baja) y guardamos el
    /// número flotante — porque las barras de vida y el spawner de números solo escuchan esos
    /// canales, no <c>OnAttributeChanged</c>. Para el resto de los stats no emitimos eventos
    /// stat-específicos (a diferencia de <c>EffAddShield</c> → <c>OnShieldChanged</c>); ese
    /// ducting se hace en effects dedicados.
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

        [Title("Clamp")]
        [SerializeField, ShowIf(nameof(TargetStat), StatType.Health)]
        [Tooltip("Si true (solo Health), el resultado se capea a la vida máxima del target. " +
                 "Evita overheal: curar a un aliado en 9/10 con +4 lo deja en 10/10, no 13/10. " +
                 "El max HP se resuelve via IEnemyAIRegistry (enemigos) o el hero del player.")]
        private bool _clampHealthToMax;

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

            return ApplyToStat(attrs, target, amount, context);
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

        private bool ApplyToStat(AttributesManager attrs, Guid target, int amount, EffectContext context) => TargetStat switch
        {
            StatType.Health       => Apply<Health>(attrs, target, amount, context),
            StatType.Attack       => Apply<Attack>(attrs, target, amount, context),
            StatType.Speed        => Apply<Speed>(attrs, target, amount, context),
            StatType.Energy       => Apply<Energy>(attrs, target, amount, context),
            StatType.Shield       => Apply<Shield>(attrs, target, amount, context),
            StatType.HealStrength => Apply<HealStrength>(attrs, target, amount, context),
            _                     => true,
        };

        private bool Apply<TAttr>(AttributesManager attrs, Guid target, int amount, EffectContext context)
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

            // Clamp opcional a la vida máxima del target (solo Health). Evita overheal
            // cuando el Healer cura aliados por encima de su tope.
            if (_clampHealthToMax && TargetStat == StatType.Health)
            {
                int maxHp = ResolveMaxHp(target);
                if (next > maxHp) next = maxHp;
                if (next < 0) next = 0;
            }

            attrs.SetAttributeValue<TAttr, int>(target, next);

            // Este effect escribe Health directo (sin IHealPipeline/IDamagePipeline), pero las
            // barras de vida (carta y ficha voladora) y el número flotante solo escuchan los
            // payloads resueltos de heal/daño. Sin emitirlos, un heal por este effect (el del
            // Healer) mutaba el dato pero ninguna vista se enteraba. Se emite con el delta REAL
            // (post-clamp), así el overheal no infla el "+N".
            if (TargetStat == StatType.Health)
                RaiseHealthDelta(context, target, before: current, after: next);

            return true;
        }

        private static void RaiseHealthDelta(EffectContext context, Guid target, int before, int after)
        {
            int delta = after - before;
            if (delta == 0) return;

            Guid source = context?.SourceGuid ?? Guid.Empty;

            if (delta > 0)
            {
                TypedEvent<HealResolvedPayload>.Raise(new HealResolvedPayload
                {
                    SourceGuid = source,
                    TargetGuid = target,
                    FinalHeal = delta,
                    WasPercentBased = false,
                });
                context?.SourceBehavior?.SetBehaviorValue(
                    BehaviorValueKey.FloatingHeal,
                    new FloatingNumberBehaviorValue { Value = delta, TargetEntityGuid = target });
            }
            else
            {
                int dmg = -delta;
                TypedEvent<DamageResolvedPayload>.Raise(new DamageResolvedPayload
                {
                    SourceGuid = source,
                    TargetGuid = target,
                    FinalDamage = dmg,
                    WasLethal = after <= 0,
                });
                context?.SourceBehavior?.SetBehaviorValue(
                    BehaviorValueKey.FloatingDamage,
                    new FloatingNumberBehaviorValue { Value = dmg, TargetEntityGuid = target });
            }
        }

        /// <summary>
        /// Max HP de referencia del target: enemigos via <see cref="IEnemyAIRegistry"/>
        /// (lo registra el spawn resolver), player via <c>CurrentHero.BaseMaxHp</c>. Sin
        /// fuente conocida ⇒ <see cref="int.MaxValue"/> (no capea). Mismo criterio que
        /// <c>RunController.BuildMaxHpResolver</c>.
        /// </summary>
        private static int ResolveMaxHp(Guid target)
        {
            if (ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry)
                && aiRegistry != null
                && aiRegistry.TryGet(target, out _, out var maxHp)
                && maxHp > 0)
            {
                return maxHp;
            }

            if (ServiceLocator.TryGetService<IPlayerService>(out var players)
                && players != null
                && players.PlayerGuid == target)
            {
                // BUG-022: incluye los grants in-run (MaxHealth.ModifiedValue), con
                // fallback interno a BaseMaxHp.
                int resolved = Rollgeon.Player.PlayerMaxHp.Resolve(target);
                if (resolved > 0) return resolved;
            }

            return int.MaxValue;
        }
    }
}
