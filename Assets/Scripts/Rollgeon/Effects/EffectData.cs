using System;
using System.Collections.Generic;
using Rollgeon.Effects.Selection;
using Rollgeon.PreConditions;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects
{
    /// <summary>
    /// Unidad atómica del pipeline: <see cref="PreConditions"/> + <see cref="Effects"/>.
    /// TECHNICAL.md §8.1.
    /// <para>
    /// Las dos listas son polimórficas e instancian con <c>new()</c> (never null por default).
    /// Regla §13.6.1 aplicada: <c>[OdinSerialize]</c> + <c>[SerializeReference]</c> para doble
    /// cobertura (Odin + Unity native) del round-trip polimórfico.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class EffectData
    {
        [PropertyOrder(-1)]
        public string Label = "Effect Group";

        [Title("Conditions", "All must pass to execute effects")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<BasePreCondition> PreConditions = new List<BasePreCondition>();

        [Title("Effects", "Executed in order (short-circuits on false via ctx.lastResult)")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize, SerializeReference]
        public List<IEffect> Effects = new List<IEffect>();

        /// <summary>
        /// Evalúa todas las precondiciones con AND semántico. TECHNICAL.md §8.1.
        /// La composición rica (OR, NOT, nested) va a través de <c>PCComposite</c>.
        /// </summary>
        public bool CanBeExecuted(PreConditionContext preCtx)
        {
            return BasePreCondition.EvaluateAll(PreConditions, preCtx);
        }

        /// <summary>
        /// Ejecuta los efectos en orden. <b>No</b> evalúa precondiciones — usa
        /// <see cref="TryExecute"/> para el flujo completo. Setea
        /// <see cref="EffectContext.EffectIndex"/> antes de cada <c>Apply</c> y respeta
        /// el cortocircuito §8.8 via <see cref="EffectContext.lastResult"/>.
        /// </summary>
        public void Execute(EffectContext ctx)
        {
            if (ctx == null || Effects == null) return;
            for (int i = 0; i < Effects.Count; i++)
            {
                var eff = Effects[i];
                if (eff == null) continue;
                ctx.EffectIndex = i;
                if (!ctx.lastResult) break;      // cortocircuito inter-efectos
                eff.Apply(ctx);                  // BaseEffect.Apply (sellada) hace su cortocircuito propio
            }
        }

        /// <summary>
        /// Flujo completo: <see cref="CanBeExecuted"/> → <see cref="Execute"/>. Devuelve
        /// <c>false</c> si las precondiciones no pasan o si el último efecto dejó
        /// <see cref="EffectContext.lastResult"/> en false.
        /// </summary>
        public bool TryExecute(EffectContext ctx, PreConditionContext preCtx)
        {
            if (!CanBeExecuted(preCtx)) return false;
            Execute(ctx);
            return ctx != null && ctx.lastResult;
        }

        /// <summary>
        /// Valida todas las selecciones requeridas por los efectos contenidos. Útil para el
        /// caller antes del <c>TryExecute</c> (§11.5). Devuelve <c>true</c> si todas pasan;
        /// en caso contrario popula <paramref name="firstError"/> con el mensaje del primer fallo.
        /// </summary>
        public bool ValidateAllSelections(TargetSelectionResult result, Guid ownerGuid, out string firstError)
        {
            firstError = null;
            if (Effects == null) return true;
            foreach (var eff in Effects)
            {
                if (eff == null) continue;
                if (!eff.ValidateSelection(result, ownerGuid, out var err))
                {
                    firstError = $"{eff.GetEffectName()}: {err}";
                    return false;
                }
            }
            return true;
        }
    }
}
