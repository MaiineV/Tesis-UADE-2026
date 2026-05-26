using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Evalúa true si <see cref="PreConditionContext.OwnerGuid"/> está a ≤ 1 tile
    /// (Chebyshev, 8 direcciones) de cualquier puerta activa de la sala actual.
    /// Puertas tapiadas (<see cref="DoorVisualState.Tapiada"/>) no cuentan.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PCAdjacentToDoor : BasePreCondition
    {
        public override string ConditionName => "AdjacentToDoor";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)) return false;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon)) return false;

            var room = dungeon.CurrentRoomInstance;
            if (room?.SpawnedPrefab == null) return false;

            if (!grid.TryGetPosition(context.OwnerGuid, out var playerCoord))
            {
                UnityEngine.Debug.LogWarning($"[PCAdjacentToDoor] Player {context.OwnerGuid:N} no tiene posición en el grid.");
                return false;
            }

            // [DIAG temporal] bug "puerta no me deja pasar". Lista el estado de cada puerta.
            UnityEngine.Debug.Log($"[PCAdjacentToDoor-DIAG] Player en {playerCoord}. Connections=[{string.Join(",", room.Connections.Keys)}]");

            foreach (var door in room.SpawnedPrefab.GetComponentsInChildren<DoorController>())
            {
                var doorCoord = grid.WorldToGrid(door.transform.position);
                bool tapiada = door.CurrentState == DoorVisualState.Tapiada;
                bool inConn = room.Connections.ContainsKey(door.Direction);
                int cheb = playerCoord.Chebyshev(doorCoord);
                UnityEngine.Debug.Log($"[PCAdjacentToDoor-DIAG] dir={door.Direction} state={door.CurrentState} " +
                    $"worldPos={door.transform.position} coord={doorCoord} inConnections={inConn} chebyshev={cheb}");

                if (tapiada) continue;
                if (!inConn) continue;
                if (cheb <= 1) return true;
            }

            UnityEngine.Debug.LogWarning($"[PCAdjacentToDoor] Player en {playerCoord} — ninguna puerta no-tapiada está a Chebyshev≤1.");
            return false;
        }
    }
}
