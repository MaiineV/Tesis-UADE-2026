using System;
using Rollgeon.Effects.Selection;
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
        /// Settings de selección embebido. Default es una instancia vacía con
        /// <c>RequiresSelection = false</c> — efectos que usan selección reemplazan
        /// esta instancia desde su constructor o via <see cref="IUsesSelection"/> marker.
        /// Visible en inspector cuando el concreto implementa <see cref="IUsesSelection"/>;
        /// la conditional rendering la aplica la editor extension downstream.
        /// </summary>
        public SelectionSettings Selection = new SelectionSettings();

        /// <summary>Default — nombre del tipo. Concretes pueden override para UX de inspector.</summary>
        public virtual string GetEffectName() => GetType().Name;

        public SelectionSettings GetSelection() => Selection;

        public bool HasSelectionRequirement()
        {
            return Selection != null && Selection.RequiresSelection;
        }

        /// <summary>
        /// Cortocircuito + grid seed + delegación a <see cref="ApplyEffect"/>. TECHNICAL.md §8.8.
        /// <b>Sellada</b> — no overridear. La lógica del efecto va en <see cref="ApplyEffect"/>.
        /// </summary>
        public bool Apply(EffectContext context)
        {
            if (context == null) return false;
            if (!context.lastResult) return false;

            // Grid seed: si el efecto declara IUsesGridSelection y no tiene RequiresSelection,
            // downstream inyecta la grilla default. La foundation no implementa ese seeding —
            // es responsabilidad de la capa que habilite grid selection agregarlo ahí.
            // Hook documentado por simetría con TECHNICAL.md §8.8.

            context.lastResult = ApplyEffect(context);
            return context.lastResult;
        }

        /// <summary>Donde el concrete hace su trabajo. Debe devolver <c>false</c> para abortar la cadena.</summary>
        public abstract bool ApplyEffect(EffectContext context);

        public bool RequiresSelectionAt(SelectionTiming timing)
        {
            return HasSelectionRequirement() && Selection.Timing == timing;
        }

        /// <summary>
        /// Default de validación de selección. TECHNICAL.md §11.5. Los concretes con reglas
        /// especiales (ej. "al menos 1 enemigo del tipo X") overridean.
        /// </summary>
        public virtual bool ValidateSelection(TargetSelectionResult result, Guid ownerGuid, out string error)
        {
            error = null;
            if (!HasSelectionRequirement()) return true;
            if (result == null) { error = "Selection result is null"; return false; }
            if (result.WasCancelled && !Selection.IsSkippable)
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
