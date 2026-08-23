using System;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Readers;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;
using UnityEngine.Serialization;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Resuelve y aplica curación al target vía <see cref="IHealPipeline"/>. Escribe el
    /// valor curado bajo <see cref="BehaviorValueKey.FloatingHeal"/> para consumo del
    /// feedback downstream. TECHNICAL.md §8.7, §9.5, §17.M.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Modos de cálculo del heal</b> (en orden de prioridad):
    /// </para>
    /// <list type="number">
    ///   <item><b>Build dice</b> (<see cref="_useBuildDice"/>): tira los 5 dados de la build
    ///   vía <see cref="IActionRollService"/> y resuelve con la fórmula N×M compartida
    ///   daño/escudo (<c>PlayerComboHeal</c>): base desde la <c>HealBaseTable</c> del
    ///   ContractSheet + ATQ + Σcaras contribuyentes × scratch × <see cref="_comboMultiplier"/>.
    ///   Sin combo, el dado holdeado más alto entra a la misma fórmula (espejo del ataque).</item>
    ///   <item><b>Dice roll genérico</b> (<see cref="_useDiceRoll"/>): NdM con
    ///   <see cref="UnityEngine.Random"/>.</item>
    ///   <item><b>Constante</b>: <see cref="_baseAmount"/> tal cual (o como % del max si
    ///   <see cref="_isPercentOfMax"/>).</item>
    /// </list>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EffHeal : BaseEffect<HealArgs, int>,
        IUsesValue, ICanBeConstantValue, IShouldStoreValuesOnBehavior,
        IActionRollEffect, IHasTooltipInfo
    {
        [Title("Heal")]
        [SerializeField]
        [Tooltip("Fuente del heal: Constant usa _baseAmount, ComboValue usa el combo, FromReader usa el reader configurado.")]
        private DamageSource _healSource = DamageSource.Constant;

        [SerializeField, ShowIf("_healSource", DamageSource.Constant)]
        [MinValue(0), MaxValue(999)]
        [Tooltip("Curación base antes de pipeline (overheal, shields). " +
                 "Es el piso del heal cuando se usa build dice.")]
        private int _baseAmount = 10;

        [SerializeField, ShowIf("@_healSource == Rollgeon.Effects.Concretes.DamageSource.ComboValue || _useBuildDice")]
        [MinValue(0.01f)]
        [Tooltip("Perilla por habilidad: en build dice escala el N entero de la fórmula N×M " +
                 "(igual que en EffDealDamage); en ComboValue multiplica el EffectiveTotal.")]
        private float _comboMultiplier = 1f;

        [OdinSerialize, SerializeReference]
        [ShowIf("_healSource", DamageSource.FromReader)]
        [Tooltip("Reader polimórfico que resuelve el heal desde stats de entidad en runtime.")]
        private EffectIntReader _reader;

        [SerializeField, ShowIf("_healSource", DamageSource.FromReader)]
        [MinValue(0.01f)]
        [Tooltip("Multiplicador aplicado al resultado del reader.")]
        private float _readerMultiplier = 1f;

        [SerializeField]
        [Tooltip("Si true, BaseAmount es porcentaje del max HP del target. " +
                 "Ignorado cuando UseBuildDice está activo.")]
        private bool _isPercentOfMax;

        [Title("Build Dice (acción Curarse)")]
        [SerializeField]
        [Tooltip("Si true, el heal usa los 5 dados de la build del player vía IActionRollService " +
                 "y resuelve con la fórmula N×M compartida (HealBaseTable + ATQ + Σcaras). " +
                 "Sin combo, cura con el dado holdeado más alto (espejo del ataque).")]
        private bool _useBuildDice;

        // DEPRECATED (Spec Heal N×M): la fórmula de umbral se reemplazó por N×M. Los tres
        // campos quedan serializados para no romper los blobs Odin de los assets existentes,
        // pero el código ya no los lee.
#pragma warning disable 0414
        [SerializeField, HideInInspector]
        private int _healThreshold = 30;

        [SerializeField, HideInInspector]
        private float _healScaleFactor = 1f;

        [SerializeField, HideInInspector]
        private int _healMaxCap = 0;
#pragma warning restore 0414

        [Title("Generic Dice (legacy / alt)")]
        [SerializeField]
        [Tooltip("Si true (y UseBuildDice false), tira DiceCount × dDiceFaces vía Random. " +
                 "Mantenido para heals NPC / scripted que no van por la build del player.")]
        private bool _useDiceRoll;

        [SerializeField, MinValue(1)]
        [ShowIf(nameof(_useDiceRoll))]
        private int _diceCount = 1;

        [SerializeField, MinValue(2)]
        [ShowIf(nameof(_useDiceRoll))]
        private int _diceFaces = 10;

        [SerializeField, MinValue(1)]
        [ShowIf(nameof(_useDiceRoll))]
        [Tooltip("Multiplicador del resultado del roll (heal = suma × multiplicador). " +
                 "Escala 100: la poción usa 10 para que 1d10 cure {10..100} preservando " +
                 "la distribución uniforme del d10 original.")]
        private int _diceResultMultiplier = 1;

        [SerializeField]
        private string _sourceTag = "eff.heal";

        [SerializeField]
        [Tooltip("Si true y no hay SelectionResult, cura al SourceGuid (self-heal).")]
        private bool _selfHealOnNoTarget = true;

        public bool UseBuildDice => _useBuildDice;

        // El inspector promete MinValue(0.01) sobre _comboMultiplier, pero un blob
        // serializado sin pasar por el inspector puede traer 0 — y en la N×M ese 0
        // multiplica TODO el heal (Curarse curaba 0 siempre, playtest 22/08). 0 no es
        // un valor autorable: se lee como "sin perilla" (×1).
        public float ComboMultiplier => _comboMultiplier > 0f ? _comboMultiplier : 1f;

        public override string GetEffectName() => "Heal";

        public bool TryGetRollSpec(Guid playerGuid, out ActionRollSpec spec)
        {
            spec = default;
            if (!_useBuildDice) return false;

            // En combate cada tirada cuesta 1 roll del pool (GDD Turn System).
            // Fuera de combate curarse es gratis — el pool no existe en exploración.
            // Threshold 0: la fórmula N×M no tiene umbral — la acción siempre cura algo
            // (con combo, tabla + ATQ + Σcaras; sin combo, el dado más alto).
            spec = new ActionRollSpec
            {
                CostsRolls = IsInCombat(),
                Threshold = 0,
                RequireConfirm = false,
                ActionLabel = "Curarse",
                AllowReroll = true,
                AlwaysSucceeds = true,
                BoardType = DiceBoardType.Default,
            };
            return true;
        }

        private static bool IsInCombat()
        {
            return ServiceLocator.TryGetService<IPhaseService>(out var phase)
                   && phase != null
                   && phase.CurrentBase == GamePhase.Combat;
        }

        // IHasTooltipInfo — solo el body: el header (nombre de la acción) y el costo
        // los agrega HeroActionTooltip.BuildFor. FromReader lee el stat configurado
        // del owner en hover-time (heals por personaje, data-driven).
        public string BuildTooltip()
            => TooltipContext.TryForCurrentHero(GamePhase.Combat, out var ctx)
                ? BuildTooltip(ctx)
                : BuildTooltip(default(TooltipContext));

        public string BuildTooltip(in TooltipContext context)
        {
            if (_useBuildDice)
            {
                // Mismo criterio que EffDealDamage/EffAddShield: la fórmula compartida,
                // con el ATQ del owner resuelto en hover-time.
                int attack = ResolveOwnerAttack(context.OwnerGuid);
                var sb = new System.Text.StringBuilder();
                sb.Append("Curación: ATQ (").Append(attack).Append(") + base del combo × multi de dados");
                if (!Mathf.Approximately(ComboMultiplier, 1f))
                    sb.Append(" × ").Append(ComboMultiplier.ToString("0.##"));
                sb.AppendLine();
                sb.Append("Sin combo: ATQ + dado más alto elegido");
                return sb.ToString();
            }

            return _healSource switch
            {
                DamageSource.FromReader when _reader != null
                    => "Curación: " + Mathf.RoundToInt(
                        _reader.Read(context.ToReaderContext()) * _readerMultiplier) + " HP",
                DamageSource.FromReader => null,
                DamageSource.ComboValue => "Curación: puntaje del combo",
                _ => _isPercentOfMax
                    ? $"Curación: {_baseAmount}% del HP máximo"
                    : $"Curación: {_baseAmount} HP",
            };
        }

        // Self-heal no requiere selección.
        public override bool HasSelectionRequirement()
        {
            return !_selfHealOnNoTarget && base.HasSelectionRequirement();
        }

        public override bool RequiresSelectionAt(Selection.SelectionTiming timing)
        {
            return !_selfHealOnNoTarget && base.RequiresSelectionAt(timing);
        }

        protected override HealArgs ResolveArgs(EffectContext context) =>
            new HealArgs { BaseAmount = ResolveBaseAmount(context) };

        protected override int ResolveValue(EffectContext context) => ResolveBaseAmount(context);

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var amount = ResolveArgs(context).BaseAmount;
            if (amount <= 0) return true;

            var targetGuid = Guid.Empty;
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord coord)
            {
                if (ServiceLocator.TryGetService<IGridManager>(out var grid))
                    grid.TryGetOccupant(coord, out targetGuid);
            }

            if (targetGuid == Guid.Empty && _selfHealOnNoTarget)
            {
                if (context.SourceEntity != null) targetGuid = context.SourceEntity.Guid;
                else if (context.SourceGuid != Guid.Empty) targetGuid = context.SourceGuid;
                else if (ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null)
                    targetGuid = ps.PlayerGuid;
            }

            if (targetGuid == Guid.Empty)
            {
                Debug.LogWarning("[EffHeal] No target resuelto aborta cadena.");
                return false;
            }

            // Build dice mode: ya pasamos por el threshold en ResolveBaseAmount, así que
            // IsPercentOfMax queda forzado a false (estamos en HP absoluto).
            bool isPercent = !_useBuildDice && _isPercentOfMax;

            int resolvedHeal = amount;
            if (ServiceLocator.TryGetService<IHealPipeline>(out var pipeline) && pipeline != null)
            {
                var healCtx = new HealContext
                {
                    SourceId = context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid,
                    TargetId = targetGuid,
                    BaseHeal = amount,
                    IsPercentOfMax = isPercent,
                    SourceTag = _sourceTag,
                };
                pipeline.Resolve(healCtx);
                resolvedHeal = healCtx.FinalHeal;
            }
            else
            {
                Debug.LogWarning("[EffHeal] IHealPipeline no registrado usando amount crudo.");
            }

            if (context.SourceBehavior != null)
            {
                context.SourceBehavior.SetBehaviorValue(
                    BehaviorValueKey.FloatingHeal,
                    new FloatingNumberBehaviorValue
                    {
                        Value = resolvedHeal,
                        TargetEntityGuid = targetGuid,
                    });
            }

            return true;
        }

        private int ResolveBaseAmount(EffectContext context = null)
        {
            // Build dice (poción) tiene prioridad sobre _healSource: la fórmula con
            // threshold/factor/cap es semánticamente distinta y consume el roll del
            // IActionRollService.
            if (_useBuildDice)
            {
                return ResolveBuildDiceAmount(context);
            }

            // Resolución estándar por fuente: Constant / ComboValue / FromReader.
            int rawAmount = _healSource switch
            {
                DamageSource.ComboValue when context?.ComboResult is { IsMatch: true } combo
                    => Mathf.RoundToInt(combo.EffectiveTotal * ComboMultiplier),
                DamageSource.ComboValue => 0,
                DamageSource.FromReader when _reader != null && context != null
                    => Mathf.RoundToInt(_reader.Read(context) * _readerMultiplier),
                DamageSource.FromReader => 0,
                _ => _baseAmount,
            };

            if (!_useDiceRoll) return rawAmount;

            int faces = Mathf.Max(2, _diceFaces);
            int count = Mathf.Max(1, _diceCount);
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += UnityEngine.Random.Range(1, faces + 1);
            }
            return ComputeDiceRollHeal(sum, _diceResultMultiplier);
        }

        // Build dice: lee context.DiceResult (populado por IActionRollService antes de
        // ejecutar el effect chain) y resuelve con la fórmula N×M compartida daño/escudo.
        // Si no hay DiceResult, fallback al base — log warning porque indica wiring roto.
        private int ResolveBuildDiceAmount(EffectContext context)
        {
            if (context?.DiceResult == null || context.DiceResult.Count == 0)
            {
                Debug.LogWarning("[EffHeal] UseBuildDice activo pero DiceResult vacío — " +
                                 "fallback a BaseAmount. Verificar que el behavior pase por IActionRollService.");
                return _baseAmount;
            }

            // Combo real (con ComboId — el sintético de action rolls viejos viene vacío):
            // base desde la HealBaseTable del sheet, gate incluido (sin entrada ⇒ 0).
            if (context.ComboResult is { IsMatch: true } combo && !string.IsNullOrEmpty(combo.ComboId))
            {
                var sheet = ServiceLocator.TryGetService<IPlayerService>(out var player)
                    ? player?.CurrentHero?.Sheet
                    : null;
                if (sheet == null) return 0;

                return Rollgeon.Combat.Damage.PlayerComboHeal.Resolve(
                    ResolveSourceId(context),
                    sheet.GetHealBase(combo.ComboId),
                    ResolveContributingDice(context, combo.ContributingIndices),
                    ComboMultiplier);
            }

            return ResolveNoComboFallback(context);
        }

        // Sin combo el heal NO es 0 — espejo de EffDealDamage.ResolveNoComboFallback:
        // el dado holdeado más alto entra UNA sola vez a la fórmula N×M (vía Σcaras con
        // detalle de bag, como comboBase sin él), sin pasar por el gate de la tabla.
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

            return Rollgeon.Combat.Damage.PlayerComboDamage.Resolve(
                ResolveSourceId(context), contributingDice != null ? 0 : max,
                contributingDice, ComboMultiplier,
                Rollgeon.Combat.Damage.PlayerComboFormulaKind.Heal);
        }

        // Guid del que cura para leer su stat Attack (término base de la fórmula
        // compartida) — mismo criterio que EffDealDamage.ResolveSourceId.
        private static Guid ResolveSourceId(EffectContext context)
            => context.SourceEntity != null ? context.SourceEntity.Guid : context.SourceGuid;

        private static int ResolveOwnerAttack(Guid ownerGuid)
        {
            if (ownerGuid == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<Rollgeon.Attributes.AttributesManager>(out var attributes)
                || attributes == null)
                return 0;
            return attributes.GetAttribute<Rollgeon.Attributes.Stats.Attack>(ownerGuid)?.ModifiedValue ?? 0;
        }

        // Mismo mecanismo que EffDealDamage: dados reales de la bag (slot + cara + tipo)
        // para los índices que formaron el combo — la fórmula pondera ESOS dados.
        private static System.Collections.Generic.IReadOnlyList<Rollgeon.Combat.Damage.ContributingDie>
            ResolveContributingDice(EffectContext context,
                System.Collections.Generic.IReadOnlyList<int> contributingIndices)
            => Rollgeon.Combat.Damage.ContributingDiceResolver.ResolveFromContext(context, contributingIndices);

        /// <summary>
        /// Heal del dice roll genérico, expuesto para tests (el roll en sí es Random):
        /// <c>suma × max(1, multiplicador)</c>. Escala 100: la poción 1d10 usa
        /// multiplicador 10 para curar {10..100}.
        /// </summary>
        public static int ComputeDiceRollHeal(int sum, int multiplier)
            => sum * Mathf.Max(1, multiplier);
    }
}
