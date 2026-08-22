using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Economy;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// La columna que engorda: marca un área telegráfica cuyo <b>ancho y daño salen del oro que
    /// lleva el jugador</b>.
    /// </summary>
    /// <remarks>
    /// El escalón efectivo es <c>clamp(rank(oro) + DamageStepUp) − DamageStepDown</c>. Sin
    /// <c>IEconomyService</c> registrado asume 0 de oro (escalón más barato) en vez de fallar: sin
    /// este nodo el jefe no ataca.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TelegraphMarkGoldScaled : AIActionNode
    {
        [Tooltip("Forma del área. El Cajero usa ColumnAroundSelf (franja vertical centrada en el " +
                 "propio jefe, así la recta sale de él y no persigue al jugador).")]
        public ThreatShape Shape = ThreatShape.ColumnAroundSelf;

        [Tooltip("Escalones por oro: desde qué oro aplica cada uno, con su ancho y su daño. " +
                 "El más barato debería arrancar en MinGold = 0.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<CashierGoldTier> Tiers = new List<CashierGoldTier>();

        [Tooltip("Tipo de ataque del DamageContext al detonar.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Tooltip("Si está activo, un soborno vigente (ICashierLedgerService.DamageStepDown) baja " +
                 "el escalón resuelto. Apagalo para probar la tabla cruda.")]
        public bool ApplyBribeStepDown = true;

        [Tooltip("Si está activo, el rastrillo (ICashierLedgerService.DamageStepUp) sube el " +
                 "escalón resuelto una vez cada N rondas, sin mirar el oro. Apagalo para probar " +
                 "la tabla cruda.")]
        public bool ApplyRakeStepUp = true;

        /// <summary>Último escalón resuelto (0-based por MinGold ascendente); -1 si nunca tickeó.</summary>
        [NonSerialized] public int LastRank = -1;

        /// <summary>Oro leído en el último tick — para el inspector de AI y logs.</summary>
        [NonSerialized] public int LastGold;

        /// <summary>Escalones que puso el rastrillo en el último tick.</summary>
        [NonSerialized] public int LastStepUp;

        public override string NodeName => $"Telegraph Mark Gold-Scaled ({Shape}, {Tiers?.Count ?? 0} tiers)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            if (Tiers == null || Tiers.Count == 0)
            {
                Debug.LogWarning("[AINode_TelegraphMarkGoldScaled] Sin escalones autorados — " +
                                 "el jefe no marcaría nada. Poblá Tiers (ver ficha del Cajero).");
                return AIResult.Failed;
            }

            int gold = 0;
            if (ServiceLocator.TryGetService<IEconomyService>(out var economy) && economy != null)
                gold = economy.CurrentGold;
            else
                Debug.LogWarning("[AINode_TelegraphMarkGoldScaled] IEconomyService no registrado — " +
                                 "se asume oro 0 (escalón más barato).");

            int stepDown = 0;
            int stepUp = 0;
            if ((ApplyBribeStepDown || ApplyRakeStepUp)
                && ServiceLocator.TryGetService<ICashierLedgerService>(out var ledger) && ledger != null)
            {
                if (ApplyBribeStepDown) stepDown = ledger.DamageStepDown;
                if (ApplyRakeStepUp) stepUp = ledger.DamageStepUp;
            }

            var tier = CashierGoldTierTable.Resolve(Tiers, gold, stepDown, stepUp, out int rank);
            if (tier == null) return AIResult.Failed;

            LastGold = gold;
            LastRank = rank;
            LastStepUp = stepUp;

            // Se publica el escalón resuelto para que la lectura del HUD muestre el daño real en vez
            // de recalcularlo con su propia copia de la tabla.
            if (ServiceLocator.TryGetService<ICashierLedgerService>(out var reportTo) && reportTo != null)
                reportTo.ReportTier(rank, tier.Damage, gold, stepUp, stepDown);

            return new AINode_TelegraphMark
            {
                Shape = Shape,
                Size = tier.ColumnSize,
                Damage = tier.Damage,
                Kind = Kind,
            }.Tick(context);
        }
    }
}
