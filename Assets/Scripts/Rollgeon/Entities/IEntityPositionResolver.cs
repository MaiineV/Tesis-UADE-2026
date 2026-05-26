using System;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Servicio opcional que mapea un <see cref="Guid"/> de entidad a su posicion
    /// mundial. Consumido por la UI de combate (<c>FloatingDamageSpawner</c>) para
    /// anclar numeros flotantes al target.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Se registra via <see cref="Patterns.ServiceLocator"/> por el enemy-spawn pipeline
    /// (T99) o el spawn del boss (T103). Si no hay implementacion registrada al
    /// momento del Bind, el spawner usa un fallback (centro de pantalla) — plan §3.8.
    /// </para>
    /// <para>
    /// Esta interface se declara en UI#0095b (plan §3.9); la implementacion real la
    /// provee el worktree dueno del pipeline de entities.
    /// </para>
    /// </remarks>
    // [STUB T99/T103] — interface declarada en UI#0095b; registro via ServiceLocator
    //                   lo hace el pipeline de entities.
    public interface IEntityPositionResolver
    {
        /// <summary>
        /// Intenta resolver la posicion mundial de <paramref name="entityId"/>.
        /// </summary>
        /// <param name="entityId">InstanceId de la entidad.</param>
        /// <returns>
        /// <see cref="Vector3"/> en coordenadas mundo si el target tiene transform
        /// visible; <c>null</c> si no existe / no tiene representacion fisica /
        /// aun no spawneo.
        /// </returns>
        Vector3? TryGetWorldPosition(Guid entityId);
    }
}
