using System;
using Rollgeon.Effects.Selection;

namespace Rollgeon.Effects
{
    /// <summary>
    /// Contrato base de cualquier efecto. TECHNICAL.md §8.3.
    /// <para>
    /// Esta interface es <b>estable</b> post-merge (plan §4.1). No se renombra ni se
    /// elimina ninguna firma — sólo se agregan métodos default en una versión interface
    /// C# 8+ si fuera necesario, minimizando la ruptura de los consumers.
    /// </para>
    /// </summary>
    public interface IEffect
    {
        /// <summary>Nombre visible en inspector / logs. Default suele ser <c>GetType().Name</c>.</summary>
        string GetEffectName();

        /// <summary>Settings de selección del efecto. Nunca null.</summary>
        SelectionSettings GetSelection();

        /// <summary>True si la selección está activa (<c>RequiresSelection == true</c>).</summary>
        bool HasSelectionRequirement();

        /// <summary>
        /// Pipeline de aplicación. La implementación base <see cref="BaseEffect.Apply"/>
        /// está <c>sealed</c> — aplica cortocircuito y delega a <c>ApplyEffect</c> (§8.8).
        /// </summary>
        bool Apply(EffectContext context);

        /// <summary>True si el efecto necesita selección resuelta al timing dado.</summary>
        bool RequiresSelectionAt(SelectionTiming timing);

        /// <summary>
        /// Valida el resultado de selección contra los requirements del efecto. Default en
        /// <see cref="BaseEffect.ValidateSelection"/> — los concretes con reglas extra overridean.
        /// </summary>
        bool ValidateSelection(TargetSelectionResult result, Guid ownerGuid, out string error);
    }
}
