using System;
using Rollgeon.Attributes;

namespace Rollgeon.Combat.Initiative
{
    /// <summary>
    /// Registro de entidades → <see cref="ModifiableAttributes"/>. Necesario
    /// para que el provider pueda resolver el stat <c>Speed</c> de una entidad
    /// dado su <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// [STUB] — el registro real de entidades migrará a
    /// <c>Rollgeon.Entities</c> cuando exista ese worktree. Se declara acá
    /// (namespace <c>Rollgeon.Combat.Initiative</c>) para no pisar el futuro
    /// namespace de Entities y para permitir compilar/testear este worktree
    /// sin bloquear por otra tarea.
    /// </para>
    /// <para>
    /// <b>Consumo.</b> Vía DI en el constructor de
    /// <see cref="DefaultInitiativeProvider"/>, nunca por
    /// <c>ServiceLocator.GetService&lt;IEntityRegistry&gt;()</c>. Así la
    /// migración futura es un cambio de namespace + find/replace, no un
    /// rewrite.
    /// </para>
    /// </remarks>
    public interface IEntityRegistry
    {
        /// <summary>
        /// Intenta recuperar el contenedor de atributos de la entidad.
        /// Devuelve <c>false</c> si el Guid no está registrado.
        /// </summary>
        bool TryGetAttributes(Guid entityId, out ModifiableAttributes attrs);
    }
}
