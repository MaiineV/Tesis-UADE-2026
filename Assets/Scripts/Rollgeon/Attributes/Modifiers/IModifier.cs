using System;
using Patterns;

namespace Rollgeon.Attributes.Modifiers
{
    /// <summary>
    /// Metadata no-generica de un <see cref="Modifier{T}"/>. Permite enumerar
    /// listas heterogeneas de modifiers para debug / HUD sin conocer el
    /// tipo del amount.
    /// </summary>
    public interface IModifier
    {
        /// <summary>GUID unico de esta instancia de modificador.</summary>
        Guid ModifierId { get; }

        /// <summary>Entidad que CARGA el modificador (quien lo lleva puesto).</summary>
        Guid CarrierId { get; }

        /// <summary>
        /// Entidad o efecto que ORIGINO el modificador. <see cref="Guid.Empty"/>
        /// si no aplica (mod auto-infligido, stat boost anonimo de tienda).
        /// </summary>
        Guid SourceId { get; }

        /// <summary>Direccion — quien dispara la consulta del modificador.</summary>
        ModifierDirection Direction { get; }

        /// <summary>Politica de vida (Turns / Permanent / Run / Encounter).</summary>
        ModifierLifetime Lifetime { get; }

        /// <summary>Operacion declarada (catalogo en <see cref="OperationResolver"/>).</summary>
        ModifierOperation Operation { get; }

        /// <summary>
        /// Evento al que se suscribe para decrementar <c>Duration</c> cuando
        /// <see cref="Lifetime"/> == <see cref="ModifierLifetime.Turns"/>.
        /// </summary>
        EventName TickEvent { get; }
    }
}
