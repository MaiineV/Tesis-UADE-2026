using System;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Servicio opcional consultado por el pipeline de feedback (§10.6) para resolver
    /// un <c>Guid</c> de entidad a un <see cref="Transform"/> de escena. Si no está
    /// registrado en el <c>ServiceLocator</c>, el resolver de posición cae al
    /// <c>WorldPosition</c> del request o al <c>Vector3.zero</c>.
    /// </summary>
    public interface IPawnRegistry
    {
        /// <summary>Registra el transform activo de un pawn. Reemplaza cualquier registro previo.</summary>
        void Register(Guid entityGuid, Transform pawn);

        /// <summary>Remueve el registro. No-op si el guid no estaba registrado.</summary>
        void Unregister(Guid entityGuid);

        /// <summary>
        /// Recupera el transform registrado. Devuelve <c>false</c> si no hay registro o si
        /// el transform fue destruido (referencia null). Los consumers deben tolerar fallas.
        /// </summary>
        bool TryGetTransform(Guid entityGuid, out Transform pawn);
    }
}
