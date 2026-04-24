using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Implementación default de <see cref="IPawnRegistry"/>. Los pawn visuals se registran
    /// en <c>OnEnable</c> / <c>Start</c> via <see cref="PawnRegistryBinding"/> o manualmente.
    /// </summary>
    public sealed class PawnRegistry : IPawnRegistry
    {
        private readonly Dictionary<Guid, Transform> _pawns = new Dictionary<Guid, Transform>();

        public void Register(Guid entityGuid, Transform pawn)
        {
            if (entityGuid == Guid.Empty || pawn == null) return;
            _pawns[entityGuid] = pawn;
        }

        public void Unregister(Guid entityGuid)
        {
            if (entityGuid == Guid.Empty) return;
            _pawns.Remove(entityGuid);
        }

        public bool TryGetTransform(Guid entityGuid, out Transform pawn)
        {
            pawn = null;
            if (entityGuid == Guid.Empty) return false;
            if (!_pawns.TryGetValue(entityGuid, out pawn)) return false;
            if (pawn == null)
            {
                _pawns.Remove(entityGuid);
                return false;
            }
            return true;
        }
    }
}
