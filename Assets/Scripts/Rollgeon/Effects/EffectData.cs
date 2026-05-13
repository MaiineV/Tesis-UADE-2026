using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Targeting;
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
        /// Override opcional del selector de target — sólo lo lee el flujo enemigo
        /// (<c>EnemyActionBehavior</c>); el héroe usa <see cref="SelectionSettings"/> y
        /// lo ignora. <c>null</c> = hereda el selector del padre (behavior, nodo).
        /// </summary>
        [Title("Enemy Target Override", "Optional. Only consumed by enemy behaviors.")]
        [OdinSerialize, SerializeReference]
        public BaseEnemyTargetSelector TargetSelector;

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
        /// Variante coroutine de <see cref="Execute"/>: después de cada efecto, yieldea hasta
        /// que <see cref="TurnManager.IsWaitingForFeedback"/> vuelva a <c>false</c>. TECHNICAL.md §10.9.
        /// </summary>
        /// <param name="ctx">Contexto del effect pass.</param>
        /// <param name="feedbackTimeoutSeconds">Cap de seguridad por-efecto para el wait.</param>
        /// <remarks>
        /// <para>
        /// Si no hay <see cref="TurnManager"/> registrado (ej. EditMode tests), se comporta igual
        /// que <see cref="Execute"/> pero expresado como coroutine — un solo <c>yield break</c>
        /// al final. Los efectos que no tocan el pipeline de feedback no pagan ningún overhead.
        /// </para>
        /// </remarks>
        public IEnumerator ExecuteCoroutine(EffectContext ctx, float feedbackTimeoutSeconds = 10f)
        {
            if (ctx == null || Effects == null) yield break;

            ServiceLocator.TryGetService<TurnManager>(out var turn);

            for (int i = 0; i < Effects.Count; i++)
            {
                var eff = Effects[i];
                if (eff == null) continue;
                ctx.EffectIndex = i;
                if (!ctx.lastResult) yield break;
                eff.Apply(ctx);

                if (turn != null && turn.IsWaitingForFeedback)
                {
                    var wait = TurnManager.WaitForFeedbackCompletion(turn, feedbackTimeoutSeconds);
                    while (wait.MoveNext()) yield return wait.Current;
                }
            }
        }

        /// <summary>
        /// Variante coroutine de <see cref="TryExecute"/>. Respeta el feedback gate entre efectos.
        /// </summary>
        public IEnumerator TryExecuteCoroutine(
            EffectContext ctx, PreConditionContext preCtx,
            System.Action<bool> onComplete, float feedbackTimeoutSeconds = 10f)
        {
            if (!CanBeExecuted(preCtx))
            {
                onComplete?.Invoke(false);
                yield break;
            }
            var co = ExecuteCoroutine(ctx, feedbackTimeoutSeconds);
            while (co.MoveNext()) yield return co.Current;
            onComplete?.Invoke(ctx != null && ctx.lastResult);
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
