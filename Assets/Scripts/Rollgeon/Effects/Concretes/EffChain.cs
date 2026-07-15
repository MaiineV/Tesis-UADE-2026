using System;
using System.Collections.Generic;
using Rollgeon.Effects.Selection;
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

        /// <summary>
        /// Selección efectiva de la fase 0 — la que el handoff usa de verdad para apuntar.
        /// El gate del botón y el hover preview pre-roll deben mirar acá, no la selection
        /// heredada del chain (oculta por <see cref="ShowSelection"/> y sin autoría posible).
        /// </summary>
        public SelectionSettings GetPhase0SelectionAt(SelectionTiming timing)
        {
            if (Phases == null || Phases.Count == 0) return null;
            return FindPhaseSelectionAt(Phases[0], timing);
        }

        /// <summary>
        /// Primera selección interactiva de una fase en el timing pedido. Recursa en
        /// chains anidados (mismo criterio que la búsqueda de EffDealDamage para la
        /// fórmula de daño). Null si ningún efecto de la fase pide selección.
        /// </summary>
        public static SelectionSettings FindPhaseSelectionAt(ChainPhase phase, SelectionTiming timing)
        {
            if (phase?.Effects?.Effects == null) return null;
            foreach (var eff in phase.Effects.Effects)
            {
                if (eff == null) continue;
                if (eff.RequiresSelectionAt(timing)) return eff.GetSelection();
                if (eff is EffChain nested)
                {
                    var inner = nested.GetPhase0SelectionAt(timing);
                    if (inner != null) return inner;
                }
            }
            return null;
        }

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
