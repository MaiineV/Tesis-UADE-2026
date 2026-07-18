using System;
using System.Collections.Generic;
using Rollgeon.Effects;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Triggers
{
    /// <summary>Evento del canal dados al que <see cref="ExecuteEffectsOnDiceEvent"/> reacciona.</summary>
    public enum EnchantmentHookEvent
    {
        /// <summary>El encantamiento se aplicó al slot (una vez).</summary>
        EnchantmentApplied,

        /// <summary>Roll crudo (post-dice, pre-reroll).</summary>
        DiceRolled,

        /// <summary>Roll final lockeado tras los rerolls.</summary>
        RollResolved,

        /// <summary>Un combo matcheó (preview — dispara en cada toggle de hold). Respeta Filter.</summary>
        ComboMatched,

        /// <summary>Fin del turno del player.</summary>
        TurnFinished,

        // APPEND-ONLY: los SOs serializan el int del enum — agregar valores solo al final.

        /// <summary>El combo se jugó (acción confirmada, pre-daño). Respeta Filter.</summary>
        ComboPlayed,
    }

    /// <summary>
    /// Trigger puente genérico del canal dados — análogo a <c>ExecuteEffectsOnEvent</c>
    /// del canal combos: ejecuta una lista de <see cref="EffectData"/> (PreConditions +
    /// cualquier <see cref="IEffect"/> del juego) cuando ocurre el <see cref="Event"/>
    /// elegido. Es el camino data-driven que reemplaza a los concretes hardcodeados.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cada <see cref="EffectData"/> corre con contexto fresco (lastResult=true) para que
    /// el cortocircuito de un grupo no bloquee al siguiente. El scratch del dispatch viaja
    /// como <see cref="ScratchTriggerContext"/>: los <c>IComboScratchWriter</c>
    /// (EffAddComboBonus, etc.) escriben al buffer del evento en curso, y las PCs
    /// carrier-aware (<c>PcCarrierFace</c>, <c>PcSlotCounterCompare</c>) leen slot y
    /// caras vía <c>PreConditionContext.Effect</c>.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ExecuteEffectsOnDiceEvent :
        IOnEnchantmentAppliedTrigger, IOnDiceRolledTrigger, IOnRollResolvedTrigger,
        IOnComboMatchedTrigger, IOnTurnFinishedTrigger, IOnComboPlayedTrigger
    {
        [Title("Event")]
        [Tooltip("Evento del canal dados que dispara los efectos.")]
        [OdinSerialize]
        public EnchantmentHookEvent Event = EnchantmentHookEvent.ComboMatched;

        [Title("Filtro de combo")]
        [InfoBox("Solo se consulta en ComboMatched / ComboPlayed.")]
        [OdinSerialize]
        public ComboFilter Filter = new ComboFilter();

        [Tooltip("Solo dispara si el dado carrier participó del combo (sus índices de " +
                 "contribución lo incluyen). Sin datos de contribución NO dispara — " +
                 "conservador. Solo aplica a ComboMatched / ComboPlayed.")]
        [OdinSerialize]
        public bool RequireCarrierParticipates;

        [Title("Effects")]
        [InfoBox("Grupos de efectos estándar (preconditions + effects), en orden, cada uno " +
                 "con contexto fresco. Los IComboScratchWriter escriben al scratch del " +
                 "dispatch; el resto aplica directo sobre los sistemas.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<EffectData> Effects = new List<EffectData>();

        public void OnEnchantmentApplied(EnchantmentTriggerContext ctx) => RunIf(EnchantmentHookEvent.EnchantmentApplied, ctx);
        public void OnDiceRolled(EnchantmentTriggerContext ctx) => RunIf(EnchantmentHookEvent.DiceRolled, ctx);
        public void OnRollResolved(EnchantmentTriggerContext ctx) => RunIf(EnchantmentHookEvent.RollResolved, ctx);
        public void OnComboMatched(EnchantmentTriggerContext ctx) => RunIf(EnchantmentHookEvent.ComboMatched, ctx);
        public void OnTurnFinished(EnchantmentTriggerContext ctx) => RunIf(EnchantmentHookEvent.TurnFinished, ctx);
        public void OnComboPlayed(EnchantmentTriggerContext ctx) => RunIf(EnchantmentHookEvent.ComboPlayed, ctx);

        private void RunIf(EnchantmentHookEvent hookEvent, EnchantmentTriggerContext ctx)
        {
            if (Event != hookEvent) return;
            if (ctx?.Effect == null || ctx.Scratch == null) return;
            if (Effects == null || Effects.Count == 0) return;

            bool isComboHook = hookEvent == EnchantmentHookEvent.ComboMatched
                            || hookEvent == EnchantmentHookEvent.ComboPlayed;
            if (isComboHook && Filter != null && !Filter.Matches(ctx.ComboId)) return;
            if (isComboHook && RequireCarrierParticipates && !CarrierParticipates(ctx)) return;

            var src = ctx.Effect;
            for (int i = 0; i < Effects.Count; i++)
            {
                var data = Effects[i];
                if (data == null) continue;

                var fx = new EffectContext
                {
                    SourceGuid = src.SourceGuid,
                    TargetGuid = src.TargetGuid != Guid.Empty ? src.TargetGuid : src.SourceGuid,
                    DiceResult = src.DiceResult,
                    KeptDice = src.KeptDice,
                    KeptDiceOriginalIndices = src.KeptDiceOriginalIndices,
                    ComboResult = src.ComboResult,
                    lastResult = true,
                    TriggerContext = new ScratchTriggerContext
                    {
                        Scratch = ctx.Scratch,
                        ComboId = ctx.ComboId,
                        Slot = ctx.Slot,
                        Channel = ScratchChannel.DiceEnchantment,
                    },
                };
                var preCtx = new PreConditionContext
                {
                    OwnerGuid = fx.SourceGuid,
                    OpponentGuid = fx.TargetGuid,
                    Effect = fx,
                };

                data.TryExecute(fx, preCtx);
            }
        }

        /// <summary>
        /// El carrier participa si algún índice de contribución del combo mapea (vía
        /// <c>KeptDiceOriginalIndices</c>) al bag index del slot. Sin mapa, los índices
        /// se asumen relativos al bag directamente.
        /// </summary>
        private static bool CarrierParticipates(EnchantmentTriggerContext ctx)
        {
            var eff = ctx.Effect;
            var combo = eff?.ComboResult;
            if (combo == null || !combo.Value.IsMatch) return false;

            var contributing = combo.Value.ContributingIndices;
            if (contributing == null || contributing.Count == 0) return false;

            var map = eff.KeptDiceOriginalIndices;
            int carrier = ctx.Slot.BagSlotIndex;
            for (int i = 0; i < contributing.Count; i++)
            {
                int j = contributing[i];
                int bagIndex = map != null && j >= 0 && j < map.Count ? map[j] : j;
                if (bagIndex == carrier) return true;
            }
            return false;
        }
    }
}
