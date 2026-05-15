using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Grid;
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

        [ToggleLeft]
        [Tooltip("Si true, mapea a uno de los 4 slots base.")]
        public bool IsBaseBehavior;

        [ShowIf(nameof(IsBaseBehavior))]
        [Tooltip("A cuál slot base corresponde.")]
        public HeroBehaviorSlot Slot;

        [MinValue(0), Range(0, 5)]
        [Tooltip("Energia cobrada al ejecutar este behavior.")]
        public int EnergyCost;

        [ToggleLeft]
        [Tooltip("Si true, no puede ejecutarse dos veces en el mismo turno.")]
        public bool BlockOnRepeat = true;

        [Title("Dice")]
        [ToggleLeft]
        [Tooltip("Si false, el behavior se ejecuta sin tirada de dados (ej. Movement).")]
        public bool NeedsDiceRoll = true;

        [ShowIf(nameof(NeedsDiceRoll))]
        [MinValue(1), Range(1, 5)]
        [Tooltip("Tiradas totales incluida la inicial. Ej: 3 = 1 roll + 2 rerolls gratis.")]
        public int FreeRollCount = 1;

        [ShowIf(nameof(NeedsDiceRoll))]
        [ToggleLeft]
        [Tooltip("Si false, el boton Reroll no aparece tras la tirada.")]
        public bool AllowsReroll = true;

        [ShowIf(nameof(NeedsDiceRoll))]
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

        public bool HasUsableEffectGroup(Guid ownerGuid, Guid opponentGuid, out string reason)
        {
            reason = null;
            if (Effects == null || Effects.Count == 0) return true;

            // Gate por EnergyCost — independiente de los preconditions del inspector.
            // Why: el campo EnergyCost ya declara el costo canónico del behavior; obligar
            // al data setup a duplicarlo via PCHasIntAttribute era frágil (se olvidaba) y
            // dejaba botones habilitados sin energía. Si IEnergyService no está registrado
            // (ej. EditMode tests), no gateamos — defensive default.
            if (EnergyCost > 0
                && ownerGuid != Guid.Empty
                && ServiceLocator.TryGetService<IEnergyService>(out var energySvc)
                && energySvc != null
                && energySvc.GetCurrent(ownerGuid) < EnergyCost)
            {
                reason = $"Not enough energy ({energySvc.GetCurrent(ownerGuid)} < {EnergyCost}).";
                return false;
            }

            var preCtx = new PreConditionContext
            {
                OwnerGuid = ownerGuid,
                OpponentGuid = opponentGuid,
                Entity = new Entity { Guid = ownerGuid },
            };

            GridCoord ownerPos = default;
            bool hasOwnerPos = ServiceLocator.TryGetService<IGridManager>(out var grid)
                               && grid.TryGetPosition(ownerGuid, out ownerPos);

            foreach (var group in Effects)
            {
                if (group == null) continue;
                if (!group.CanBeExecuted(preCtx)) continue;

                bool groupUsable = true;
                if (group.Effects != null)
                {
                    foreach (var eff in group.Effects)
                    {
                        if (eff == null) continue;
                        if (!eff.RequiresSelectionAt(SelectionTiming.BeforeRoll)) continue;

                        if (!hasOwnerPos)
                        {
                            groupUsable = false;
                            break;
                        }

                        var targets = eff.GetSelection().ResolveValidTiles(ownerPos, ownerGuid);
                        if (targets == null || targets.Count == 0)
                        {
                            groupUsable = false;
                            break;
                        }
                    }
                }

                if (groupUsable) return true;
            }

            reason = "No usable effect group: all groups have failing preconditions or no valid targets.";
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
                effCtx.ActionRollEffectiveTotal = heroCtx.ActionRollEffectiveTotal;
            }

            return effCtx;
        }

        public EffDealDamage FindFirstDealDamageEffect()
        {
            if (Effects == null) return null;
            foreach (var group in Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is EffDealDamage dealDmg) return dealDmg;
                }
            }
            return null;
        }

        public EffChain FindChainEffect()
        {
            if (Effects == null) return null;
            foreach (var group in Effects)
            {
                if (group?.Effects == null) continue;
                foreach (var eff in group.Effects)
                {
                    if (eff is EffChain chain) return chain;
                }
            }
            return null;
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
