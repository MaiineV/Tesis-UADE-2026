using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dice;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
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
        IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior, IHasTooltipInfo
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

        /// <summary>Tipo de ataque con el que este daño entra al pipeline.</summary>
        public AttackKind Kind => _attackKind;

        /// <summary>
        /// El daño que el panel de intención puede afirmar hoy para <paramref name="ownerGuid"/>:
        /// el mismo número que entraría a <c>DamageContext.BaseDamage</c>. ComboValue depende de
        /// una tirada que todavía no pasó — ahí no hay nada que afirmar.
        /// </summary>
        public bool TryDescribePreviewDamage(Guid ownerGuid, out int damage)
        {
            switch (_damageSource)
            {
                case DamageSource.Constant:
                    damage = _baseAmount;
                    return damage > 0;
                case DamageSource.FromReader when _reader != null:
                    damage = Mathf.RoundToInt(
                        _reader.Read(new EffectContext { SourceGuid = ownerGuid }) * _readerMultiplier);
                    return damage > 0;
                default:
                    damage = 0;
                    return false;
            }
        }

        public override string GetEffectName() => "Deal Damage";

        // IHasTooltipInfo — texto dinámico por personaje: ComboValue lee el ATQ del
        // owner en hover-time; FromReader ejecuta el reader configurado (ej. un stat
        // del character) con un contexto mínimo. Expansión de personajes = data-driven.
        public string BuildTooltip()
            => TooltipContext.TryForCurrentHero(Rollgeon.Phase.GamePhase.Combat, out var ctx)
                ? BuildTooltip(ctx)
                : BuildTooltip(default(TooltipContext));

        public string BuildTooltip(in TooltipContext context)
        {
            switch (_damageSource)
            {
                case DamageSource.ComboValue:
                {
                    int attack = ResolveOwnerAttack(context.OwnerGuid);
                    var sb = new System.Text.StringBuilder();
                    sb.Append(string.Format(
                        LocalizedContent.Ui("tooltip.effect.damage.combo_header",
                            "Daño: ATQ ({0}) + puntaje del combo"), attack));
                    if (!Mathf.Approximately(_comboMultiplier, 1f))
                        sb.Append(string.Format(
                            LocalizedContent.Ui("tooltip.effect.combo.multiplier_suffix", " × {0}"),
                            _comboMultiplier.ToString("0.##")));
                    sb.AppendLine();
                    sb.Append(LocalizedContent.Ui("tooltip.effect.combo.no_combo_fallback",
                        "Sin combo: ATQ + dado más alto elegido"));
                    return sb.ToString();
                }
                case DamageSource.FromReader when _reader != null:
                    return string.Format(
                        LocalizedContent.Ui("tooltip.effect.damage.flat", "Daño: {0}"),
                        Mathf.RoundToInt(_reader.Read(context.ToReaderContext()) * _readerMultiplier));
                case DamageSource.FromReader:
                    return null;
                default:
                    return string.Format(
                        LocalizedContent.Ui("tooltip.effect.damage.flat", "Daño: {0}"), _baseAmount);
            }
        }

        private static int ResolveOwnerAttack(Guid ownerGuid)
        {
            if (ownerGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attributes)
                || attributes == null)
                return 0;
            return attributes.GetAttribute<Attack>(ownerGuid)?.ModifiedValue ?? 0;
        }

        protected override DamageArgs ResolveArgs(EffectContext context)
        {
            int amount = _damageSource switch
            {
                // Ver PlayerComboDamage.Resolve para la fórmula completa.
                DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
                    => Rollgeon.Combat.Damage.PlayerComboDamage.Resolve(
                        ResolveSourceId(context), combo.BaseDamage,
                        ResolveContributingDice(context, combo.ContributingIndices), _comboMultiplier),
                // Sin combo el ataque NO es 0 — daño mínimo del GD §5 ("número más
                // alto"): el dado más alto de los holdeados entra a la misma fórmula
                // (v3: UNA sola vez, vía Σcaras cuando hay detalle de bag).
                DamageSource.ComboValue => ResolveNoComboFallback(context),
                DamageSource.FromReader when _reader != null
                    => Mathf.RoundToInt(_reader.Read(context) * _readerMultiplier),
                DamageSource.FromReader => 0,
                _ => _baseAmount,
            };

            return new DamageArgs { BaseAmount = amount };
        }

        // Prioriza KeptDice (los dados que el jugador eligió) sobre DiceResult (la tirada
        // completa): si holdeó solo un 3 y quedó un 6 sin holdear, el mínimo es 3, no 6.
        // Sin dados (behavior ComboValue sin tirada) sigue siendo 0, como antes.
        private int ResolveNoComboFallback(EffectContext context)
        {
            var dice = context?.KeptDice ?? context?.DiceResult;
            if (dice == null || dice.Count == 0) return 0;

            int max = 0;
            int maxIndex = -1;
            for (int i = 0; i < dice.Count; i++)
                if (dice[i] > max) { max = dice[i]; maxIndex = i; }
            if (max <= 0) return 0;

            var contributingDice = maxIndex >= 0
                ? ResolveContributingDice(context, new[] { maxIndex })
                : null;

            // v3: la cara del dado más alto entra UNA sola vez a N. Con detalle de bag va
            // por Σcaras (comboBase 0, conserva la atribución del dado para la UI); sin bag
            // entra como comboBase — mismo N por ambos caminos.
            return Rollgeon.Combat.Damage.PlayerComboDamage.Resolve(
                ResolveSourceId(context), contributingDice != null ? 0 : max,
                contributingDice, _comboMultiplier);
        }

        private static IReadOnlyList<Rollgeon.Combat.Damage.ContributingDie> ResolveContributingDice(
            EffectContext context, IReadOnlyList<int> contributingIndices)
            => Rollgeon.Combat.Damage.ContributingDiceResolver.ResolveFromContext(context, contributingIndices);

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
                        // Combo → habilita la etapa de weakness del pipeline. El combo que
                        // matcheó decide qué debilidad del target dispara (o ninguna → ×1.0).
                        ComboId = context.ComboResult?.ComboId,
                        IsWeaknessHit = context.ComboResult?.IsMatch ?? false,
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

        // Guid del atacante para leer su stat Attack (daño base del PJ). Mismo criterio que ApplyEffect.
        private static Guid ResolveSourceId(EffectContext context)
            => context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;

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
