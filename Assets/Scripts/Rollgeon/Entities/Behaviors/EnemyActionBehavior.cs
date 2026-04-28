using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Effects;
using Rollgeon.Effects.Selection;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Behavior componible del enemigo. Paralelo a <c>HeroActionBehavior</c>: arma una
    /// lista de <see cref="EffectData"/> (PC + Effects) que se ejecutan en orden, con
    /// short-circuit por grupo. La diferencia con el héroe es que el target lo decide un
    /// <see cref="BaseEnemyTargetSelector"/> polimórfico — no la UI.
    /// </summary>
    /// <remarks>
    /// El tree es quien gobierna cuándo se invoca este behavior (via <c>AINode_Behavior</c>),
    /// por eso <see cref="CanExecute"/> queda en <c>true</c> y los chequeos finos viven en
    /// las PC del tree y de cada <see cref="EffectData"/>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class EnemyActionBehavior : BaseBehavior
    {
        [Title("Action Config")]
        [Tooltip("Nombre visible en logs.")]
        public string ActionName = "Enemy Action";

        [Title("Targeting")]
        [Tooltip("Selector usado por defecto. Cada EffectData puede overridear el suyo. " +
                 "Null = AlwaysPlayer (resuelve via EnemyTargetResolver).")]
        [OdinSerialize, SerializeReference]
        public BaseEnemyTargetSelector TargetSelector;

        [Title("Effect Pipeline")]
        [InfoBox("Grupos de PreConditions + Effects ejecutados en orden. " +
                 "Short-circuit: si un grupo retorna false, los siguientes no corren.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<EffectData> Effects = new List<EffectData>();

        public override string BehaviorName => ActionName;

        // The tree decides when to fire this behavior — gate by tree PCs, not by trigger soft-check.
        public override bool CanExecute(BehaviorContext ctx) => true;

        public override void Execute(BehaviorContext ctx)
        {
            ClearBehaviorValues();

            var enemyCtx = ctx as EnemyAIBehaviorContext;
            if (enemyCtx == null || enemyCtx.AI == null)
            {
                Debug.LogWarning("[EnemyActionBehavior] Expected EnemyAIBehaviorContext with AIContext set.");
                return;
            }
            if (Effects == null || Effects.Count == 0) return;

            var aiCtx = enemyCtx.AI;
            var ownerGuid = aiCtx.SelfGuid;
            var behaviorTarget = EnemyTargetResolver.Resolve(TargetSelector, aiCtx, ownerGuid);

            foreach (var ed in Effects)
            {
                if (ed == null) continue;
                var setTarget = ed.TargetSelector != null
                    ? EnemyTargetResolver.Resolve(ed.TargetSelector, aiCtx, ownerGuid)
                    : behaviorTarget;

                var preCtx = aiCtx.BuildPcContext(setTarget);
                if (!ed.CanBeExecuted(preCtx)) continue;

                var effCtx = BuildEffectContext(aiCtx, setTarget, ctx.SourceEntity, ctx.TriggeringEntity);
                ed.Execute(effCtx);
            }
        }

        private EffectContext BuildEffectContext(AIContext aiCtx, Guid targetGuid, Entity source, Entity trigger)
        {
            var effCtx = new EffectContext
            {
                SourceGuid = aiCtx.SelfGuid,
                TargetGuid = targetGuid,
                SourceEntity = source ?? aiCtx.Self,
                TriggeringEntity = trigger,
                SourceBehavior = this,
                TriggerContext = new EnemyAIBehaviorContext { AI = aiCtx, SourceEntity = source ?? aiCtx.Self },
                lastResult = true,
            };

            // Inject precomputed target so effects with autoresolve flow read it via SelectionResult.
            // EffDealDamage already prefers SelectionResult over ctx.TargetGuid, so this keeps both
            // paths consistent without forcing the enemy to go through SelectionSettings.
            if (targetGuid != Guid.Empty
                && Patterns.ServiceLocator.TryGetService<Rollgeon.Grid.IGridManager>(out var grid)
                && grid != null
                && grid.TryGetPosition(targetGuid, out var coord))
            {
                effCtx.SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(coord) },
                };
            }

            return effCtx;
        }
    }
}
