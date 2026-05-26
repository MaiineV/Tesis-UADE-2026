using System;
using System.Collections.Generic;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffChain : BaseEffect
    {
        protected override bool ShowSelection => false;

        [Title("Chain Phases")]
        [InfoBox("Cada fase tiene su propia tirada de dados. Free rolls y energia " +
                 "se pasan del budget restante entre fases. Si una fase arranca con " +
                 "0 free rerolls y 0 energia, se auto-termina el chain.\n\n" +
                 "Selection por fase: configurar el Selection de cada efecto DENTRO " +
                 "de la fase (Timing, SlotState, etc). EffChain no tiene selection propia.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<ChainPhase> Phases = new List<ChainPhase>();

        public int PhaseCount => Phases?.Count ?? 0;

        public override string GetEffectName() => "Chain";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null || Phases == null || Phases.Count == 0) return false;

            var preCtx = BuildFallbackPreCtx(context);

            for (int i = 0; i < Phases.Count; i++)
            {
                var phase = Phases[i];
                if (phase?.Effects == null) continue;
                phase.Effects.TryExecute(context, preCtx);
                if (!context.lastResult) break;
            }

            return context.lastResult;
        }

        private static PreConditionContext BuildFallbackPreCtx(EffectContext ctx)
        {
            return new PreConditionContext
            {
                OwnerGuid = ctx.SourceGuid,
                OpponentGuid = ctx.TargetGuid,
                Entity = ctx.SourceEntity,
            };
        }
    }
}
