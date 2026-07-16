using System;
using System.Collections.Generic;
using Rollgeon.Effects;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Combos.Triggers.Concretes
{
    /// <summary>Evento de juego al que <see cref="ExecuteEffectsOnEvent"/> reacciona.</summary>
    public enum ComboPassiveHookEvent
    {
        /// <summary>El combo target de la pasiva matcheó (requiere TargetComboId).</summary>
        ComboMatched,
        TurnStarted,
        TurnFinished,
        RoomEntered,
        CombatStart,
        CombatEnd,
        GoldChanged,
        DiceRolled,
        RollResolved,
        DamageResolved,
    }

    /// <summary>
    /// Trigger puente genérico: ejecuta una lista de <see cref="EffectData"/> (el
    /// pipeline estándar de Effects — preconditions + cualquier <see cref="IEffect"/>
    /// del juego) cuando ocurre el <see cref="Event"/> elegido. Es el camino
    /// data-driven para que un combo passive haga CUALQUIER cosa que el sistema de
    /// efectos sepa hacer, sin escribir un trigger nuevo por comportamiento.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A diferencia de los triggers de scratch (<see cref="AddGoldOnComboMatch"/>,
    /// etc.), acá los efectos se aplican DIRECTO sobre los sistemas (vía
    /// <c>EffectData.TryExecute</c>) — no pasan por el <c>EnchantmentScratch</c>.
    /// </para>
    /// <para>
    /// Cada <see cref="EffectData"/> corre con un <see cref="EffectContext"/> fresco
    /// (lastResult=true) para que el cortocircuito de un grupo no bloquee al
    /// siguiente. Target default = el propio source (self-cast) cuando el evento no
    /// trae target explícito.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ExecuteEffectsOnEvent :
        IOnComboPassiveMatchedTrigger,
        IOnTurnStartedPassiveTrigger, IOnTurnFinishedPassiveTrigger,
        IOnRoomEnteredPassiveTrigger,
        IOnCombatStartPassiveTrigger, IOnCombatEndPassiveTrigger,
        IOnGoldChangedPassiveTrigger,
        IOnDiceRolledPassiveTrigger, IOnRollResolvedPassiveTrigger,
        IOnDamageResolvedPassiveTrigger
    {
        [Title("Event")]
        [Tooltip("Evento de juego que dispara los efectos. ComboMatched respeta el " +
                 "TargetComboId de la pasiva; el resto son globales (cualquier pasiva).")]
        [OdinSerialize]
        public ComboPassiveHookEvent Event = ComboPassiveHookEvent.ComboMatched;

        [Title("Effects")]
        [InfoBox("Grupos de efectos estándar (preconditions + effects). Se ejecutan en orden; " +
                 "cada grupo corre con contexto fresco. Los efectos aplican directo sobre los " +
                 "sistemas (no via scratch).")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<EffectData> Effects = new List<EffectData>();

        public void OnComboMatched(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.ComboMatched, ctx);
        public void OnTurnStarted(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.TurnStarted, ctx);
        public void OnTurnFinished(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.TurnFinished, ctx);
        public void OnRoomEntered(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.RoomEntered, ctx);
        public void OnCombatStart(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.CombatStart, ctx);
        public void OnCombatEnd(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.CombatEnd, ctx);
        public void OnGoldChanged(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.GoldChanged, ctx);
        public void OnDiceRolled(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.DiceRolled, ctx);
        public void OnRollResolved(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.RollResolved, ctx);
        public void OnDamageResolved(ComboPassiveContext ctx) => RunIf(ComboPassiveHookEvent.DamageResolved, ctx);

        private void RunIf(ComboPassiveHookEvent hookEvent, ComboPassiveContext ctx)
        {
            if (Event != hookEvent) return;
            if (ctx?.Effect == null || Effects == null || Effects.Count == 0) return;

            var src = ctx.Effect;
            for (int i = 0; i < Effects.Count; i++)
            {
                var data = Effects[i];
                if (data == null) continue;

                // Contexto fresco por grupo — mismo shape que InventoryService arma
                // para los hooks de items: lastResult=true, target default = source.
                var fx = new EffectContext
                {
                    SourceGuid = src.SourceGuid,
                    TargetGuid = src.TargetGuid != Guid.Empty ? src.TargetGuid : src.SourceGuid,
                    DiceResult = src.DiceResult,
                    KeptDice = src.KeptDice,
                    ComboResult = src.ComboResult,
                    lastResult = true,
                };
                var preCtx = new PreConditionContext
                {
                    OwnerGuid = fx.SourceGuid,
                    OpponentGuid = fx.TargetGuid,
                };

                data.TryExecute(fx, preCtx);
            }
        }
    }
}
