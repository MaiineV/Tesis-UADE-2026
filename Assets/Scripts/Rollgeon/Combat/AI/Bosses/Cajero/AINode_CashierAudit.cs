using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Cashier;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// "Arqueo de caja" (fase al 50% de HP del Cajero): guarda <see cref="TaxPercent"/> del oro del
    /// jugador en la caja del jefe, se cura con eso hasta <see cref="MaxHeal"/>, y a partir de ahí
    /// las fichas valen <see cref="ChipValueMultiplierAfterAudit"/> veces más.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No es un robo, es un secuestro.</b> El oro no desaparece: queda en
    /// <c>ICashierLedgerService.VaultedGold</c> y vuelve completo al jugador cuando el jefe muere
    /// (el servicio escucha <c>OnEntityDestroyed</c>). Si el jugador muere primero, gana la banca.
    /// Es el único punto del kit del Cajero que escribe oro del jugador, y está en la ficha.
    /// </para>
    /// <para>
    /// <b>Siempre Succeeded si pudo correr</b> (incluso cobrando 0 porque el jugador está seco):
    /// va envuelto en <c>Once → Sequence[Audit, ApplyStatModifier]</c>, y un Failed acá abortaría
    /// la secuencia y dejaría la Fase 2 sin su feedback — el jefe se quedaría sin anunciar el
    /// cambio para siempre porque <c>Once</c> no latchea con Failed.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierAudit : AIActionNode
    {
        [Tooltip("Fracción (0..1) del oro del jugador que se guarda en la caja. Ficha: 0.40.")]
        [Range(0f, 1f)]
        public float TaxPercent = 0.4f;

        [Tooltip("Tope de curación del jefe, en HP. Cura min(oro guardado, este tope). Ficha: 30.")]
        [MinValue(0)]
        public int MaxHeal = 30;

        [Tooltip("Multiplicador del valor de las fichas después del arqueo. Ficha: 2.")]
        [MinValue(1)]
        public int ChipValueMultiplierAfterAudit = 2;

        public override string NodeName => $"Cashier Audit ({TaxPercent:P0} → heal ≤ {MaxHeal})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var attrs = context.Attributes;
            if (attrs == null) ServiceLocator.TryGetService<AttributesManager>(out attrs);

            var ledger = CashierLedgerService.ResolveOrCreate();

            int collected = ledger.CollectTax(context.SelfGuid, TaxPercent);
            ledger.SetChipValueMultiplier(ChipValueMultiplierAfterAudit);

            int heal = Mathf.Min(collected, MaxHeal);
            if (heal > 0) ApplyHeal(attrs, context, heal);

            return AIResult.Succeeded;
        }

        private static void ApplyHeal(AttributesManager attrs, AIContext context, int heal)
        {
            if (attrs == null)
            {
                Debug.LogWarning("[AINode_CashierAudit] AttributesManager no disponible — se guardó " +
                                 "el oro pero el jefe no se cura.");
                return;
            }

            // SelfMaxHp es el cap del spawn (misma fuente que PcOwnerHpBelow). Sin baseline no se
            // clampea: preferimos curar de más que perder la curación del arqueo entero.
            int maxHp = context.SelfMaxHp > 0 ? context.SelfMaxHp : int.MaxValue;
            attrs.Modify<Health, int>(context.SelfGuid, current =>
            {
                int healed = current + heal;
                return healed > maxHp ? maxHp : healed;
            });

            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                context.SelfGuid,
                FloatingNumberType.Heal,
                (float)heal,
                Vector3.zero);
        }
    }
}
