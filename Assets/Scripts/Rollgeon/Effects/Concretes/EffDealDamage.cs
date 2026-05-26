using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Resuelve y aplica daño al target vía <see cref="IDamagePipeline"/>. Escribe el
    /// daño final bajo <see cref="BehaviorValueKey.FloatingDamage"/> para que un
    /// <c>EffPlayFeedback</c> downstream pueda mostrar el número flotante.
    /// TECHNICAL.md §8.7, §9.5, §12.2.
    /// </summary>
    /// <remarks>
    /// Atómico: resuelve el target desde <see cref="EffectContext.SelectionResult"/>
    /// (si hay) o <see cref="EffectContext.TargetGuid"/> como fallback. Si <see cref="IDamagePipeline"/>
    /// no está registrado en el <c>ServiceLocator</c>, loguea warning y devuelve <c>false</c>
    /// — cortocircuita la cadena (§8.8).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffDealDamage : BaseEffect<DamageArgs, int>,
        IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior
    {
        [Title("Damage")]
        [SerializeField]
        [Tooltip("Fuente del daño base: Constant usa _baseAmount, ComboValue usa el resultado del combo.")]
        private DamageSource _damageSource = DamageSource.Constant;

        [SerializeField, ShowIf("_damageSource", DamageSource.Constant)]
        [MinValue(0), MaxValue(999)]
        [Tooltip("Daño base antes de pipeline (mitigación, críticos, weakness).")]
        private int _baseAmount = 10;

        [SerializeField, ShowIf("_damageSource", DamageSource.ComboValue)]
        [MinValue(0.01f)]
        [Tooltip("Multiplicador aplicado al BaseDamage del combo resuelto.")]
        private float _comboMultiplier = 1f;

        [OdinSerialize, SerializeReference]
        [ShowIf("_damageSource", DamageSource.FromReader)]
        [Tooltip("Reader polimórfico que resuelve el daño desde stats de entidad en runtime.")]
        private EffectIntReader _reader;

        [SerializeField, ShowIf("_damageSource", DamageSource.FromReader)]
        [MinValue(0.01f)]
        [Tooltip("Multiplicador aplicado al resultado del reader.")]
        private float _readerMultiplier = 1f;

        [SerializeField]
        [Tooltip("Tipo de ataque usado al construir el DamageContext.")]
        private AttackKind _attackKind = AttackKind.BasicAttack;

        public DamageSource Source => _damageSource;
        public float ComboMultiplier => _comboMultiplier;
        public int BaseAmount => _baseAmount;

        public override string GetEffectName() => "Deal Damage";

        protected override DamageArgs ResolveArgs(EffectContext context)
        {
            int amount = _damageSource switch
            {
                DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
                    => Mathf.RoundToInt(combo.BaseDamage * _comboMultiplier),
                DamageSource.ComboValue => 0,
                DamageSource.FromReader when _reader != null
                    => Mathf.RoundToInt(_reader.Read(context) * _readerMultiplier),
                DamageSource.FromReader => 0,
                _ => _baseAmount,
            };

            // Consumimos los bonuses de combo passives (tienda) y dice enchantments
            // que se acumulan en LastComboScratch.BonusComboDamage durante OnComboMatched.
            // Solo aplica cuando hay combo matcheado — sin combo, no hay bonus que sumar.
            if (_damageSource == DamageSource.ComboValue
                && context?.ComboResult is { IsMatch: true })
            {
                if (ServiceLocator.TryGetService<IComboPassiveService>(out var passives) && passives?.LastComboScratch != null)
                    amount += passives.LastComboScratch.BonusComboDamage;
                if (ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants) && enchants?.LastComboScratch != null)
                    amount += enchants.LastComboScratch.BonusComboDamage;
            }

            return new DamageArgs { BaseAmount = amount };
        }

        protected override int ResolveValue(EffectContext context) => ResolveArgs(context).BaseAmount;

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var amount = ResolveArgs(context).BaseAmount;
            if (amount <= 0) return true;

            var targets = ResolveAllTargetGuids(context);
            Debug.Log($"[EffDealDamage] ApplyEffect — resolved {targets.Count} target(s), selectedTargets={context.SelectionResult?.SelectedTargets?.Count ?? 0}, fallbackTargetGuid={context.TargetGuid}");
            for (int i = 0; i < targets.Count; i++)
                Debug.Log($"[EffDealDamage]   target[{i}] = {targets[i]}");
            if (targets.Count == 0) return true;

            var sourceId = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;
            ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline);

            foreach (var targetGuid in targets)
            {
                int resolvedDamage = amount;
                if (pipeline != null)
                {
                    var dmgCtx = new DamageContext
                    {
                        SourceId = sourceId,
                        TargetId = targetGuid,
                        BaseDamage = amount,
                        Kind = _attackKind,
                    };
                    pipeline.Resolve(dmgCtx);
                    resolvedDamage = dmgCtx.FinalDamage;
                }

                if (context.SourceBehavior != null)
                {
                    context.SourceBehavior.SetBehaviorValue(
                        BehaviorValueKey.FloatingDamage,
                        new FloatingNumberBehaviorValue
                        {
                            Value = resolvedDamage,
                            TargetEntityGuid = targetGuid,
                        });
                }
            }

            return true;
        }

        private static System.Collections.Generic.List<Guid> ResolveAllTargetGuids(EffectContext context)
        {
            var result = new System.Collections.Generic.List<Guid>();

            if (context.SelectionResult?.SelectedTargets != null
                && ServiceLocator.TryGetService<Grid.IGridManager>(out var grid))
            {
                foreach (var target in context.SelectionResult.SelectedTargets)
                {
                    if (grid.TryGetOccupant(target.Coord, out var occupant) && occupant != Guid.Empty)
                        result.Add(occupant);
                }
            }

            if (result.Count == 0 && context.TargetGuid != Guid.Empty)
                result.Add(context.TargetGuid);

            return result;
        }
    }
}
