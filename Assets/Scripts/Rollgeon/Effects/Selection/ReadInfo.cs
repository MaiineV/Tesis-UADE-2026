using System;

namespace Rollgeon.Effects.Selection
{
    /// <summary>
    /// Payload mínimo que <see cref="SelectionSettings.GetSelectionCount"/> necesita para
    /// resolver la cantidad de targets dinámica (p.ej. "1 por nivel del caster").
    /// En esta foundation sólo lleva el <see cref="ownerGuid"/>; los readers downstream
    /// extienden el struct si necesitan más campos (sin breaking, porque <c>readonly</c>
    /// struct no cambia firmas de métodos — los consumers que no leen nuevos campos no ven).
    /// </summary>
    public struct ReadInfo
    {
        public Guid ownerGuid;
    }
}
