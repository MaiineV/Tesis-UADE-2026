using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Damage;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.Localization;
using Rollgeon.Player;
using Rollgeon.UI.Tooltips;
using Rollgeon.Upgrades.Dice;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    [Serializable, HideReferenceObjectPicker]
    public class EffAddShield : BaseEffect<ShieldArgs, int>,
        IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior, IHasTooltipInfo
    {
        [Title("Shield")]
        [SerializeField]
        [Tooltip("Source: Constant usa _baseAmount; ComboValue usa la fórmula compartida " +
                 "daño/escudo v3 (ATQ + tabla escudo_combo_base del ContractSheet × multi " +
                 "de dados × scratch, sin cap).")]
        private DamageSource _shieldSource = DamageSource.Constant;

        [SerializeField, ShowIf("_shieldSource", DamageSource.Constant)]
        [MinValue(0), MaxValue(999)]
        private int _baseAmount = 5;

        [SerializeField, ShowIf("_shieldSource", DamageSource.ComboValue)]
        [MinValue(0.01f)]
        [Tooltip("Perilla por habilidad: escala solo el término base del combo (igual que " +
                 "en EffDealDamage). ATQ y bonos planos no se multiplican.")]
        private float _comboMultiplier = 1f;

        [OdinSerialize, SerializeReference]
        [ShowIf("_shieldSource", DamageSource.FromReader)]
        [Tooltip("Reader polimórfico que resuelve el shield desde stats de entidad en runtime.")]
        private EffectIntReader _reader;

        [SerializeField, ShowIf("_shieldSource", DamageSource.FromReader)]
        [MinValue(0.01f)]
        [Tooltip("Multiplicador aplicado al resultado del reader.")]
        private float _readerMultiplier = 1f;

        public DamageSource ShieldSource => _shieldSource;
        public int BaseAmount => _baseAmount;

        /// <summary>Setter de autoría (tooling/tests): fuente Constant con el monto dado.</summary>
        public void EditorSetConstantAmount(int amount)
        {
            _shieldSource = DamageSource.Constant;
            _baseAmount = amount;
        }

        /// <summary>
        /// Setter de autoría (tooling/tests): fuente FromReader — el escudo lo resuelve el
        /// reader en runtime (ej. <c>ReadTilesTraversed</c> para Baluarte móvil).
        /// </summary>
        public void EditorSetReader(EffectIntReader reader, float multiplier = 1f)
        {
            _shieldSource = DamageSource.FromReader;
            _reader = reader;
            _readerMultiplier = multiplier;
        }
        public float ComboMultiplier => _comboMultiplier;

        public override string GetEffectName() => "Add Shield";

        // IHasTooltipInfo — mismo criterio que EffDealDamage: valores dinámicos cuando
        // la fuente lo permite (FromReader lee stats del owner en hover-time).
        public string BuildTooltip()
            => TooltipContext.TryForCurrentHero(Rollgeon.Phase.GamePhase.Combat, out var ctx)
                ? BuildTooltip(ctx)
                : BuildTooltip(default(TooltipContext));

        public string BuildTooltip(in TooltipContext context)
        {
            switch (_shieldSource)
            {
                case DamageSource.ComboValue:
                {
                    int attack = ResolveOwnerAttack(context.OwnerGuid);
                    var sb = new System.Text.StringBuilder();
                    sb.Append(string.Format(
                        LocalizedContent.Ui("tooltip.effect.shield.combo_header",
                            "Escudo: ATQ ({0}) + base del combo × multi de dados"), attack));
                    if (!Mathf.Approximately(_comboMultiplier, 1f))
                        sb.Append(string.Format(
                            LocalizedContent.Ui("tooltip.effect.combo.multiplier_suffix", " × {0}"),
                            _comboMultiplier.ToString("0.##")));
                    return sb.ToString();
                }
                case DamageSource.FromReader when _reader != null:
                    return string.Format(
                        LocalizedContent.Ui("tooltip.effect.shield.flat", "Escudo: +{0}"),
                        Mathf.RoundToInt(_reader.Read(context.ToReaderContext()) * _readerMultiplier));
                case DamageSource.FromReader:
                    return null;
                default:
                    return string.Format(
                        LocalizedContent.Ui("tooltip.effect.shield.flat", "Escudo: +{0}"), _baseAmount);
            }
        }

        protected override ShieldArgs ResolveArgs(EffectContext context)
        {
            int amount = _shieldSource switch
            {
                // El escudo NUNCA lee combo.BaseDamage (tabla de ATAQUE — causa raíz del
                // bug de escudo trivial, BUG-021). Del ComboResult solo se usan los dados
                // contribuyentes; la base sale de la tabla de escudo por clase.
                DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
                    => ResolveComboShield(context, combo),
                DamageSource.ComboValue => 0,
                DamageSource.FromReader when _reader != null
                    => Mathf.RoundToInt(_reader.Read(context) * _readerMultiplier),
                DamageSource.FromReader => 0,
                _ => _baseAmount,
            };
            return new ShieldArgs { BaseAmount = amount };
        }

        // Sheet del player como fuente de verdad de la tabla de escudo — mismo criterio
        // que ActionRollService. Un ComboId vacío (resultado sintético de action roll)
        // significa "sin datos por combo": no genera escudo.
        private int ResolveComboShield(EffectContext context, ComboDetectionResult combo)
        {
            var sheet = ServiceLocator.TryGetService<IPlayerService>(out var player)
                ? player?.CurrentHero?.Sheet
                : null;
            if (sheet == null || string.IsNullOrEmpty(combo.ComboId)) return 0;

            return PlayerComboShield.Resolve(
                ResolveSourceId(context),
                sheet.GetShieldBase(combo.ComboId),
                ResolveContributingDice(context, combo.ContributingIndices),
                _comboMultiplier);
        }

        // Guid del dueño del escudo para leer su stat Attack (término base de la fórmula
        // compartida) — mismo criterio que EffDealDamage.ResolveSourceId.
        private static Guid ResolveSourceId(EffectContext context)
            => context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;

        private static int ResolveOwnerAttack(Guid ownerGuid)
        {
            if (ownerGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attributes)
                || attributes == null)
                return 0;
            return attributes.GetAttribute<Attack>(ownerGuid)?.ModifiedValue ?? 0;
        }

        // Mismo mecanismo que EffDealDamage: dados reales de la bag (slot + cara + tipo)
        // para los índices que formaron el combo — la fórmula pondera ESOS dados.
        private static IReadOnlyList<ContributingDie> ResolveContributingDice(
            EffectContext context, IReadOnlyList<int> contributingIndices)
            => ContributingDiceResolver.ResolveFromContext(context, contributingIndices);

        protected override int ResolveValue(EffectContext context) => ResolveArgs(context).BaseAmount;

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var amount = ResolveArgs(context).BaseAmount;
            if (amount <= 0) return true;

            var targetGuid = ResolveTargetGuid(context);

            if (targetGuid == Guid.Empty)
            {
                Debug.LogWarning("[EffAddShield] No target resolved — aborting chain.");
                return false;
            }

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attributes)
                || attributes == null)
            {
                Debug.LogWarning("[EffAddShield] AttributesManager not registered.");
                return false;
            }

            var shieldAttr = attributes.GetAttribute<Shield>(targetGuid);
            int current = shieldAttr?.Value ?? 0;
            int newShield = current + amount;

            attributes.SetAttributeValue<Shield, int>(targetGuid, newShield);
            EventManager.Trigger(EventName.OnShieldChanged, targetGuid, newShield);

            if (context.SourceBehavior != null)
            {
                context.SourceBehavior.SetBehaviorValue(
                    BehaviorValueKey.FloatingShield,
                    new FloatingNumberBehaviorValue
                    {
                        Value = amount,
                        TargetEntityGuid = targetGuid,
                    });
            }
            else
            {
                // Canal dados / items (Baluarte móvil al caminar): sin behavior no hay
                // FeedbackDB que pinte el número, así que el "+N" sale directo por la ruta
                // legacy del spawner — el jugador tiene que ver cuándo el escudo sumó.
                EventManager.Trigger(EventName.OnFloatingNumberRequested, targetGuid,
                    Rollgeon.UI.HUD.FloatingNumberType.Shield, (float)amount, Vector3.zero);
            }

            return true;
        }

        private static Guid ResolveTargetGuid(EffectContext context)
        {
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord coord
                && ServiceLocator.TryGetService<IGridManager>(out var grid)
                && grid.TryGetOccupant(coord, out var occupant)
                && occupant != Guid.Empty)
                return occupant;
            return context.SourceGuid;
        }
    }
}
