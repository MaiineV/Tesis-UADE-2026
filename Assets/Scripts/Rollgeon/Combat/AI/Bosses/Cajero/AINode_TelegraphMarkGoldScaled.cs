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
    /// lleva el jugador</b>. Es el nodo central del Cajero (piso 2) — su único vector de daño y
    /// su anzuelo económico: cada ficha que levantás lo acerca al escalón siguiente.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Composición, no herencia.</b> Delega en un <see cref="AINode_TelegraphMark"/> armado en
    /// el momento con el Size/Damage del escalón resuelto — el mismo truco que usa
    /// <c>HazardService</c> para reusar los nodos de telegraph. Así el nodo compartido no se toca
    /// y cualquier arreglo de shapes/overlay llega gratis.
    /// </para>
    /// <para>
    /// <b>Tres <c>If(PcGoldCompare)</c> harían lo mismo… hasta el soborno.</b> El escalón efectivo
    /// es <c>rank(oro) − DamageStepDown</c>, y con Ifs sueltos cada rama necesitaría además saber
    /// si hay soborno activo (3 gates × 2 estados). Un nodo con la tabla adentro deja el árbol en
    /// un solo hijo legible y la matemática en <see cref="CashierGoldTierTable"/>, testeable sin
    /// grilla ni servicios.
    /// </para>
    /// <para>
    /// <b>Sin economía registrada</b> asume 0 de oro (escalón más barato) en vez de fallar: el
    /// jefe sin este nodo no ataca, y un combate donde nadie amenaza es peor bug que un golpe
    /// flojo. <c>PcGoldCompare</c> es no-permisivo por la razón inversa (no habilitar gastos).
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TelegraphMarkGoldScaled : AIActionNode
    {
        [Tooltip("Forma del área. El Cajero usa Column (franja vertical centrada en el jugador).")]
        public ThreatShape Shape = ThreatShape.Column;

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

        /// <summary>Último escalón resuelto (0-based por MinGold ascendente); -1 si nunca tickeó.
        /// Estado de debug por pelea — el árbol se clona por combate.</summary>
        [NonSerialized] public int LastRank = -1;

        /// <summary>Oro leído en el último tick — para el inspector de AI y logs.</summary>
        [NonSerialized] public int LastGold;

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
            if (ApplyBribeStepDown
                && ServiceLocator.TryGetService<ICashierLedgerService>(out var ledger) && ledger != null)
                stepDown = ledger.DamageStepDown;

            var tier = CashierGoldTierTable.Resolve(Tiers, gold, stepDown, out int rank);
            if (tier == null) return AIResult.Failed;

            LastGold = gold;
            LastRank = rank;

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
