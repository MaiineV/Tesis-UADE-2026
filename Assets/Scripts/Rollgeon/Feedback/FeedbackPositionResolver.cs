using System;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Feedback
{
    /// <summary>
    /// Switch puro sobre <see cref="SpawnPosition"/> para convertir la intención autoral
    /// en una posición mundial. Siempre suma <c>entry.PositionOffset</c> al final.
    /// TECHNICAL.md §10.6.
    /// </summary>
    public static class FeedbackPositionResolver
    {
        /// <summary>
        /// Resuelve la posición final para <paramref name="entry"/> sumando
        /// <paramref name="entry.PositionOffset"/>. Fallbacks silenciosos — los callers
        /// nunca deberían pegar <c>NullReferenceException</c> por este camino.
        /// </summary>
        public static Vector3 Resolve(
            FeedbackEntry entry,
            Guid sourceGuid,
            Guid targetGuid,
            Vector3 worldPositionHint,
            FeedbackPlayer player = FeedbackPlayer.Player)
        {
            if (entry == null) return Vector3.zero;

            Vector3 basePos;
            switch (entry.Position)
            {
                case SpawnPosition.AtSource:
                    basePos = ResolveForGuid(sourceGuid, worldPositionHint);
                    break;
                case SpawnPosition.AtTarget:
                    basePos = ResolveForGuid(targetGuid, worldPositionHint);
                    break;
                case SpawnPosition.AtSlot:
                    basePos = ResolveSlot(targetGuid, worldPositionHint);
                    break;
                case SpawnPosition.BetweenSourceAndTarget:
                {
                    var a = ResolveForGuid(sourceGuid, worldPositionHint);
                    var b = ResolveForGuid(targetGuid, worldPositionHint);
                    basePos = Vector3.Lerp(a, b, 0.5f);
                    break;
                }
                case SpawnPosition.WorldPosition:
                    basePos = worldPositionHint;
                    break;
                case SpawnPosition.FromReader:
                    basePos = ResolveFromReader(entry, player, worldPositionHint);
                    break;
                default:
                    basePos = worldPositionHint;
                    break;
            }

            return basePos + entry.PositionOffset;
        }

        /// <summary>Convenience — devuelve el Transform del target si existe.</summary>
        public static Transform ResolvePawnTransform(Guid guid)
        {
            if (guid == Guid.Empty) return null;
            if (!ServiceLocator.TryGetService<IPawnRegistry>(out var reg) || reg == null) return null;
            return reg.TryGetTransform(guid, out var t) ? t : null;
        }

        private static Vector3 ResolveForGuid(Guid guid, Vector3 fallback)
        {
            if (guid == Guid.Empty) return fallback;

            if (ServiceLocator.TryGetService<IPawnRegistry>(out var reg) && reg != null
                && reg.TryGetTransform(guid, out var t) && t != null)
                return t.position;

            return ResolveSlot(guid, fallback);
        }

        private static Vector3 ResolveSlot(Guid guid, Vector3 fallback)
        {
            if (guid == Guid.Empty) return fallback;

            if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null
                && grid.TryGetPosition(guid, out var coord))
                return grid.GridToWorld(coord);

            return fallback;
        }

        private static Vector3 ResolveFromReader(FeedbackEntry entry, FeedbackPlayer player, Vector3 fallback)
        {
            if (entry.PositionReaderSO is IPositionReader reader)
                return reader.Read(new PositionReadInfo { Player = player });
            return fallback;
        }
    }
}
