using System;
using System.Collections.Generic;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Heroes
{
    [Serializable, HideReferenceObjectPicker]
    public class HeroActionBehavior : BaseBehavior
    {
        [Title("Action Config")]
        [Tooltip("Nombre visible en UI y logs.")]
        public string ActionName = "Action";

        [MinValue(0), Range(0, 5)]
        [Tooltip("Energia cobrada al ejecutar este behavior.")]
        public int EnergyCost;

        [ToggleLeft]
        [Tooltip("Si true, no puede ejecutarse dos veces en el mismo turno.")]
        public bool BlockOnRepeat = true;

        [Title("Dice")]
        [MinValue(1), Range(1, 5)]
        [Tooltip("Tiradas totales incluida la inicial. Ej: 3 = 1 roll + 2 rerolls gratis.")]
        public int FreeRollCount = 1;

        [ToggleLeft]
        [Tooltip("Si false, el boton Reroll no aparece tras la tirada.")]
        public bool AllowsReroll = true;

        [ToggleLeft]
        [Tooltip("Permite gastar energia para re-rolls extra mas alla del budget gratis.")]
        public bool AllowsEnergyReroll = true;

        [Title("Show Conditions")]
        [InfoBox("PreConditions que determinan si el boton de este behavior aparece en la UI. " +
                 "Vacio = siempre visible. Usado para behaviors contextuales (ej. 'hay puerta adyacente').")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<BasePreCondition> ShowConditions = new List<BasePreCondition>();

        [Title("Effect Pipeline")]
        [InfoBox("Grupos de PreConditions + Effects ejecutados en orden. " +
                 "Short-circuit: si un grupo retorna false, los siguientes no corren.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<EffectData> Effects = new List<EffectData>();

        public override string BehaviorName => ActionName;

        public bool ShouldShow(PreConditionContext preCtx)
        {
            return BasePreCondition.EvaluateAll(ShowConditions, preCtx);
        }

        public bool HasEffectsWithSelection()
        {
            if (Effects == null) return false;
            foreach (var group in Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff != null && eff.HasSelectionRequirement())
                        return true;
                }
            }
            return false;
        }

        public bool HasEffectsWithSelectionAt(SelectionTiming timing)
        {
            if (Effects == null) return false;
            foreach (var group in Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff != null && eff.RequiresSelectionAt(timing))
                        return true;
                }
            }
            return false;
        }

        public override void Execute(BehaviorContext ctx)
        {
            ClearBehaviorValues();

            if (Effects == null || Effects.Count == 0) return;

            var effCtx = BuildEffectContext(ctx);
            var preCtx = BuildPreConditionContext(ctx);

            foreach (var group in Effects)
            {
                if (group == null) continue;
                group.TryExecute(effCtx, preCtx);
                if (!effCtx.lastResult) break;
            }
        }

        private EffectContext BuildEffectContext(BehaviorContext ctx)
        {
            var effCtx = new EffectContext
            {
                SourceGuid = ctx?.SourceEntity != null ? ctx.SourceEntity.Guid : Guid.Empty,
                SourceEntity = ctx?.SourceEntity,
                TriggeringEntity = ctx?.TriggeringEntity,
                SourceBehavior = this,
                TriggerContext = ctx,
            };

            if (ctx is HeroBehaviorContext heroCtx)
            {
                effCtx.DiceResult = heroCtx.DiceResult;
                effCtx.ComboResult = heroCtx.MatchedComboResult;
                effCtx.TargetGuid = heroCtx.TargetGuid;
                effCtx.SelectionResult = heroCtx.SelectionResult;
            }

            return effCtx;
        }

        private PreConditionContext BuildPreConditionContext(BehaviorContext ctx)
        {
            var preCtx = new PreConditionContext
            {
                OwnerGuid = ctx?.SourceEntity != null ? ctx.SourceEntity.Guid : Guid.Empty,
                Entity = ctx?.SourceEntity,
            };

            if (ctx is HeroBehaviorContext heroCtx)
                preCtx.OpponentGuid = heroCtx.TargetGuid;

            return preCtx;
        }
    }
}
