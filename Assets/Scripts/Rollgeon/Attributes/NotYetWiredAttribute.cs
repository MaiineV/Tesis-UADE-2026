using System;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// Marker inerte para bloques autorables que compilan y se pueden configurar, pero cuyo efecto
    /// mecánico todavía no está wireado — hoy son no-ops o sólo loguean.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El problema que resuelve: un diseñador arma un encantamiento con
    /// <c>WildcardForCombo</c>, lo tunea, lo mete en un pool, lo playtestea — y no pasa nada,
    /// porque <c>ContractSheet</c> todavía no consume el flag. Nada en el inspector lo delata; hoy
    /// la única fuente es <c>docs/balance/item-inventory.html</c>, mantenido a mano.
    /// </para>
    /// <para>
    /// Mismo patrón que <see cref="HiddenFromUIAttribute"/>: marker sin comportamiento, leído por
    /// reflexión desde las tools de autoría, que lo muestran como warning junto al bloque.
    /// </para>
    /// <para>
    /// <b>Aplicarlo sólo cuando el bloque es realmente un no-op.</b> Un <c>TODO Phase 4</c> en el
    /// código no alcanza: <c>LuckyChanceComboBonus</c> tiene uno (por el seed determinístico) pero
    /// sí suma su bonus, y marcarlo mentiría. El criterio es si el jugador nota la diferencia.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class NotYetWiredAttribute : Attribute
    {
        /// <summary>Qué falta para que funcione. Se muestra tal cual al diseñador.</summary>
        public string Reason { get; }

        public NotYetWiredAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
