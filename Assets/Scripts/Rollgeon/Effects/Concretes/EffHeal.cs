using System;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
using Rollgeon.Phase;
using Rollgeon.Player;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
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
    ///   vía <see cref="IActionRollService"/>. Si <c>sum &gt;= </c><see cref="_healThreshold"/>
    ///   → heal = <see cref="_baseAmount"/> + (sum - threshold). Si no → heal = base.</item>
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
        [SerializeField, MinValue(0), MaxValue(999)]
        [Tooltip("Curación base. Es el piso del heal cuando se usa build dice.")]
        private int _baseAmount = 10;

        [SerializeField]
        [Tooltip("Si true, BaseAmount es porcentaje del max HP del target. " +
                 "Ignorado cuando UseBuildDice está activo.")]
        private bool _isPercentOfMax;

        [Title("Build Dice (poción)")]
        [SerializeField]
        [Tooltip("Si true, el heal usa los 5 dados de la build del player y aplica la fórmula " +
                 "de umbral (heal = base + max(0, sum - threshold)). Ruta el flujo a través " +
                 "de IActionRollService cuando se invoca como acción de hero.")]
        private bool _useBuildDice;

        [SerializeField, MinValue(0)]
        [ShowIf(nameof(_useBuildDice))]
        [Tooltip("Threshold del 'effective total' de la tirada (formula B: combo.BaseDamage " +
                 "si hay combo, sino suma cruda). Si lo alcanza, el excedente se suma al heal " +
                 "base. Default 30 — alineado con Force Door para que requiera al menos un Trio.")]
        private int _healThreshold = 30;

        [SerializeField, MinValue(0f)]
        [ShowIf(nameof(_useBuildDice))]
        [Tooltip("Factor de escala: HP ganados por cada punto del puntaje por encima del " +
                 "umbral. Referencias por fase: Early 0.8, Mid 0.3, Late 0.08, Endgame 0.02. " +
                 "Default 1.0 mantiene la fórmula lineal simple.")]
        private float _healScaleFactor = 1f;

        [SerializeField, MinValue(0)]
        [ShowIf(nameof(_useBuildDice))]
        [Tooltip("Tope máximo absoluto de curación (red de seguridad ante puntajes altos). " +
                 "0 = sin cap. El designer convierte el % HP máx (Early 25, Mid 40, Late 55, " +
                 "Endgame 65) a valor absoluto según el HP esperado del jugador en esa fase.")]
        private int _healMaxCap = 0;

        [SerializeField, MinValue(0)]
        [FormerlySerializedAs("_energyCost")]
        [ShowIf(nameof(_useBuildDice))]
        [Tooltip("Energía que cuesta intentar curarse DENTRO de combate. Fuera de " +
                 "combate es 0 (la poción no cuesta energía explorando). Cobrado por " +
                 "IActionRollService al iniciar la tirada. Default 2.")]
        private int _energyCostInCombat = 2;

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

        [SerializeField]
        private string _sourceTag = "eff.heal";

        [SerializeField]
        [Tooltip("Si true y no hay SelectionResult, cura al SourceGuid (self-heal).")]
        private bool _selfHealOnNoTarget = true;

        public override string GetEffectName() => "Heal";

        public bool TryGetRollSpec(Guid playerGuid, out ActionRollSpec spec)
        {
            spec = default;
            if (!_useBuildDice) return false;

            // En combate cuesta _energyCostInCombat. Fuera de combate la poción no
            // gasta energía (igual que EffForceDoor con su EnergyCostInCombat).
            int cost = IsInCombat() ? _energyCostInCombat : 0;

            spec = new ActionRollSpec
            {
                EnergyCost = cost,
                Threshold = _healThreshold,
                RequireConfirm = false,
                ActionLabel = "Curarse",
                AllowReroll = true,
                RerollEnergyCost = 1,
                AlwaysSucceeds = true,
            };
            return true;
        }

        private static bool IsInCombat()
        {
            return ServiceLocator.TryGetService<IPhaseService>(out var phase)
                   && phase != null
                   && phase.CurrentBase == GamePhase.Combat;
        }

        // IHasTooltipInfo — el binder de la pocion consume esto. Solo emite texto
        // cuando _useBuildDice esta on: en modo constante / generic dice no hay
        // umbral ni tope relevantes para mostrar al jugador.
        public string BuildTooltip()
        {
            if (!_useBuildDice) return null;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Poción de Curación</b>");
            sb.Append("HP Base: ").Append(_baseAmount);
            sb.AppendLine();
            sb.Append("Umbral Mínimo: ").Append(_healThreshold);
            if (_healMaxCap > 0)
            {
                sb.AppendLine();
                sb.Append("Tope Máximo: ").Append(_healMaxCap).Append(" HP");
            }
            return sb.ToString();
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

        private int ResolveBaseAmount(EffectContext context)
        {
            if (_useBuildDice)
            {
                return ResolveBuildDiceAmount(context);
            }

            if (!_useDiceRoll) return _baseAmount;

            int faces = Mathf.Max(2, _diceFaces);
            int count = Mathf.Max(1, _diceCount);
            int sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += UnityEngine.Random.Range(1, faces + 1);
            }
            return sum;
        }

        // Build dice: lee context.DiceResult (populado por IActionRollService antes de
        // ejecutar el effect chain). Si no hay DiceResult, fallback al base — log
        // warning porque indica wiring roto.
        private int ResolveBuildDiceAmount(EffectContext context)
        {
            if (context?.DiceResult == null || context.DiceResult.Count == 0)
            {
                Debug.LogWarning("[EffHeal] UseBuildDice activo pero DiceResult vacío — " +
                                 "fallback a BaseAmount. Verificar que el behavior pase por IActionRollService.");
                return _baseAmount;
            }

            // Prioridad: el ActionRollService ya computó el effective sobre los held dice.
            // Si viene pre-computado, usarlo — sino caemos al cálculo legacy (combo o suma
            // cruda de los 5), que sobrestima el heal cuando el user holdeó pocos dados.
            int effectiveTotal = context.ActionRollEffectiveTotal
                ?? ActionRollTotals.ResolveEffectiveTotal(context.DiceResult, context.ComboResult);

            return ComputeBuildDiceHeal(_baseAmount, _healThreshold, effectiveTotal,
                _healScaleFactor, _healMaxCap);
        }

        /// <summary>
        /// Fórmula expuesta para tests:
        /// <list type="bullet">
        ///   <item><c>score &lt; healThreshold</c> → <c>heal = base</c> (sin escalado).</item>
        ///   <item><c>score &gt;= healThreshold</c> → <c>heal = base + floor((score - threshold) × scaleFactor)</c>.</item>
        ///   <item>Si <paramref name="maxCap"/> &gt; 0, el resultado se clampea a <paramref name="maxCap"/>.</item>
        /// </list>
        /// Spec: HP = HP_Base + ((Puntaje - Umbral_Mínimo) × Factor_de_Escala), floor abajo, cap por Tope_Máximo.
        /// </summary>
        public static int ComputeBuildDiceHeal(int baseAmount, int healThreshold, int score,
            float scaleFactor, int maxCap)
        {
            int heal;
            if (score < healThreshold)
            {
                heal = baseAmount;
            }
            else
            {
                int bonus = Mathf.FloorToInt((score - healThreshold) * Mathf.Max(0f, scaleFactor));
                heal = baseAmount + bonus;
            }

            if (maxCap > 0 && heal > maxCap) heal = maxCap;
            return heal;
        }
    }
}
