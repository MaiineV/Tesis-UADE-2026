using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Cruza al player por la puerta de la sala actual que esté adyacente a su
    /// posición (Chebyshev ≤ 1, 8 direcciones). No requiere configurar la dirección
    /// en el Inspector — la detecta en runtime igual que <c>PCAdjacentToDoor</c>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffPassDoor : BaseEffect
    {
        public override string GetEffectName() => "Pass Door";

        public override bool ApplyEffect(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid))
            {
                Debug.LogWarning("[EffPassDoor] IGridManager no registrado — no-op.");
                return false;
            }

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon))
            {
                Debug.LogWarning("[EffPassDoor] IDungeonService no registrado — no-op.");
                return false;
            }

            var room = dungeon.CurrentRoomInstance;
            if (room?.SpawnedPrefab == null)
            {
                Debug.LogWarning("[EffPassDoor] No hay sala activa con prefab — no-op.");
                return false;
            }

            if (!grid.TryGetPosition(context.SourceGuid, out var playerCoord))
            {
                Debug.LogWarning("[EffPassDoor] Player sin posición en el grid — no-op.");
                return false;
            }

            Debug.Log($"[EffPassDoor] Player en {playerCoord}. Buscando puertas en '{room.SpawnedPrefab.name}'.");
            foreach (var door in room.SpawnedPrefab.GetComponentsInChildren<DoorController>())
            {
                if (door.CurrentState == DoorVisualState.Tapiada) continue;
                var doorCoord = grid.WorldToGrid(door.transform.position);
                Debug.Log($"[EffPassDoor] Puerta dir={door.Direction} state={door.CurrentState} coord={doorCoord} dist={playerCoord.Chebyshev(doorCoord)}");
                if (playerCoord.Chebyshev(doorCoord) <= 1)
                    return dungeon.EnterRoomByDoor(door.Direction);
            }

            Debug.LogWarning("[EffPassDoor] Ninguna puerta adyacente encontrada — no-op.");
            return false;
        }
    }
}
