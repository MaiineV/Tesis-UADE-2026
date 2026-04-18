using System;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// Marker inerte para stats que el HUD (§D) debe skippear al iterar los
    /// atributos de una entidad para render.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Autoridad: TECHNICAL.md §4.2 — <c>Speed</c> se declara "oculta en UI".
    /// TECHNICAL.md §12.7 reafirma: "ningún listener debe leer directamente el
    /// <c>Speed</c> para exponerlo al jugador". Este atributo formaliza la regla
    /// como un contrato C# auditable (se puede verificar por reflexión).
    /// </para>
    /// <para>
    /// El enforcement real llega con T95a/T95b (HUD) — cuando iteren stats
    /// para render, los marcados con <see cref="HiddenFromUIAttribute"/> no se
    /// pintan. Este worktree sólo deja el marker aplicado a
    /// <c>Rollgeon.Attributes.Stats.Speed</c>.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class HiddenFromUIAttribute : Attribute
    {
    }
}
