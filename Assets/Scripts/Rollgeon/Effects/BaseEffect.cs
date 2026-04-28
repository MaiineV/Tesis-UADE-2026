using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects
{
    /// <summary>
    /// Base no genérica de cualquier efecto. TECHNICAL.md §8.3 + §8.8.
    /// <para>
    /// Regla de serialización polimórfica (§13.6.1): esta clase es <c>abstract</c> y lleva
    /// <c>[Serializable, HideReferenceObjectPicker]</c>. Los contenedores (<see cref="EffectData.Effects"/>)
    /// deben marcar <c>[OdinSerialize]</c> + <c>[SerializeReference]</c> en el campo contenedor.
    /// </para>
    /// <para>
    /// El método <see cref="Apply"/> está <b>sellado</b> para garantizar el cortocircuito §8.8 —
    /// los concretes overridean <see cref="ApplyEffect"/> únicamente.
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class BaseEffect : IEffect
    {
        /// <summary>
        /// Settings de selección embebido. Todos los efectos tienen config de selección
        /// (puede ser Self, AutoResolve, o interactiva).
        /// </summary>
        [ShowIf(nameof(ShowSelection))]
        public SelectionSettings Selection = new SelectionSettings();

        protected virtual bool ShowSelection => true;

        /// <summary>Default — nombre del tipo. Concretes pueden override para UX de inspector.</summary>
        public virtual string GetEffectName() => GetType().Name;

        public SelectionSettings GetSelection() => Selection;

        public bool HasSelectionRequirement()
        {
            return Selection != null && Selection.NeedsPlayerInteraction();
        }

        /// <summary>
        /// Cortocircuito + grid seed + delegación a <see cref="ApplyEffect"/>. TECHNICAL.md §8.8.
        /// <b>Sellada</b> — no overridear. La lógica del efecto va en <see cref="ApplyEffect"/>.
        /// </summary>
        public bool Apply(EffectContext context)
        {
            if (context == null) return false;
            if (!context.lastResult) return false;

            context.lastResult = ApplyEffect(context);
            return context.lastResult;
        }

        /// <summary>Donde el concrete hace su trabajo. Debe devolver <c>false</c> para abortar la cadena.</summary>
        public abstract bool ApplyEffect(EffectContext context);

        public bool RequiresSelectionAt(SelectionTiming timing)
        {
            return Selection != null && Selection.NeedsSelectionAt(timing);
        }

        /// <summary>
        /// Id de la entry en el <c>FeedbackDBSO</c> que este effect dispara. Default vacío.
        /// Effects con <see cref="IUsesFeedback"/> overridean (o exponen un <c>[SerializeField]</c>
        /// en su propio tipo y lo devuelven acá). TECHNICAL.md §10.9.
        /// </summary>
        public virtual string GetFeedbackId() => null;

        /// <summary>
        /// Arma un <see cref="FeedbackRequest"/> para este effect, leyendo los ids del
        /// contexto y snapshotteando el bag de <c>context.SourceBehavior.StoredValues</c>
        /// (§9.5 — el snapshot desacopla al request del lifecycle del behavior).
        /// TECHNICAL.md §10.9.
        /// </summary>
        public virtual FeedbackRequest GetFeedbackRequest(EffectContext context)
        {
            var req = new FeedbackRequest
            {
                FeedbackId = GetFeedbackId(),
            };

            if (context == null) return req;

            req.SourceGuid = context.SourceGuid;
            var resolvedTarget = context.TargetGuid;
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord coord
                && ServiceLocator.TryGetService<IGridManager>(out var grid)
                && grid.TryGetOccupant(coord, out var occupant)
                && occupant != Guid.Empty)
                resolvedTarget = occupant;
            req.TargetGuid = resolvedTarget;
            req.StoredValues = SnapshotStoredValues(context.SourceBehavior);
            return req;
        }

        /// <summary>
        /// Copia defensiva del bag de un behavior — referencia al dict original rompe la
        /// regla de §9.5 ("snapshot, no referencia viva"). Devuelve <c>null</c> si el
        /// behavior es null o no tiene valores.
        /// </summary>
        private static IReadOnlyDictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>>
            SnapshotStoredValues(BaseBehavior behavior)
        {
            if (behavior == null) return null;

            var copy = new Dictionary<BehaviorValueKey, List<BaseBehaviorStoredValue>>();
            foreach (BehaviorValueKey key in Enum.GetValues(typeof(BehaviorValueKey)))
            {
                if (key == BehaviorValueKey.None) continue;
                if (behavior.TryGetBehaviorValues<BaseBehaviorStoredValue>(key, out var list) && list.Count > 0)
                {
                    copy[key] = new List<BaseBehaviorStoredValue>(list);
                }
            }
            return copy.Count > 0 ? copy : null;
        }

        public virtual bool ValidateSelection(TargetSelectionResult result, Guid ownerGuid, out string error)
        {
            error = null;
            if (Selection == null || !Selection.NeedsPlayerInteraction()) return true;
            if (result == null) { error = "Selection result is null"; return false; }
            if (result.WasCancelled)
            {
                error = "Selection cancelled";
                return false;
            }

            if (result.WasCompleted)
            {
                var required = Selection.GetSelectionCount(new ReadInfo { ownerGuid = ownerGuid });
                var count = result.SelectedTargets != null ? result.SelectedTargets.Count : 0;
                if (count < required)
                {
                    error = $"Expected {required}, got {count}";
                    return false;
                }
            }
            return true;
        }
    }
}
