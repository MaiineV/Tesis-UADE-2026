using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Handoff;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Rollgeon.Player;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// <see cref="IEnemySpawnCoordOverride"/> del Tutorial Mode: en la sala B (el
    /// primer combate) planta al melee a pocas casillas del jugador, para que la
    /// lección de MOVER se resuelva con un solo movimiento. Prefiere casillas
    /// alineadas con el player (misma fila/columna) a distancia
    /// <see cref="TargetDistance"/>; el resto de las salas usa el default.
    /// </summary>
    public sealed class TutorialEnemySpawnPlacement : IEnemySpawnCoordOverride
    {
        /// <summary>Distancia Manhattan buscada player→enemigo: cerca para llegar
        /// de un movimiento, pero lejos como para que mover sea necesario.</summary>
        private const int TargetDistance = 3;

        private readonly Guid _firstCombatRoomId;

        public TutorialEnemySpawnPlacement(Guid firstCombatRoomId)
        {
            _firstCombatRoomId = firstCombatRoomId;
        }

        /// <summary>Factory: registra la instancia en <see cref="ServiceScope.Run"/>.</summary>
        public static TutorialEnemySpawnPlacement CreateAndRegister(Guid firstCombatRoomId)
        {
            var placement = new TutorialEnemySpawnPlacement(firstCombatRoomId);
            ServiceLocator.AddService<IEnemySpawnCoordOverride>(placement, ServiceScope.Run);
            return placement;
        }

        public bool TryOverrideSpawnCoord(
            RoomInstance instance, int spawnIndex, GridCoord defaultCoord, out GridCoord coord)
        {
            coord = defaultCoord;
            if (instance == null || instance.InstanceId != _firstCombatRoomId) return false;

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid)
                || grid?.Graph == null) return false;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player) || player == null
                || !grid.TryGetPosition(player.PlayerGuid, out var playerCoord)) return false;

            var doorCoords = CollectDoorCoords(instance, grid);

            // Mejor candidata: walkable, libre, no-puerta; distancia lo más cerca
            // posible de TargetDistance (nunca adyacente — mover tiene que hacer
            // falta), alineada con el player si se puede.
            GridCoord? best = null;
            int bestScore = int.MaxValue;
            foreach (var candidate in grid.Graph.AllCoords())
            {
                if (!grid.IsWalkable(candidate) || grid.IsOccupied(candidate)) continue;
                if (doorCoords.Contains(candidate)) continue;

                int distance = Math.Abs(candidate.X - playerCoord.X) + Math.Abs(candidate.Y - playerCoord.Y);
                if (distance < 2) continue;

                bool aligned = candidate.X == playerCoord.X || candidate.Y == playerCoord.Y;
                int score = Math.Abs(distance - TargetDistance) * 10 + (aligned ? 0 : 1);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (!best.HasValue) return false;
            coord = best.Value;
            return true;
        }

        private static HashSet<GridCoord> CollectDoorCoords(RoomInstance instance, IGridManager grid)
        {
            var set = new HashSet<GridCoord>();
            var layout = instance.SpawnedPrefab != null
                ? instance.SpawnedPrefab.GetComponent<RoomLayout>()
                : null;
            if (layout?.DoorSlots == null) return set;

            foreach (var slot in layout.DoorSlots)
            {
                if (slot?.Anchor == null) continue;
                set.Add(grid.WorldToGrid(slot.Anchor.position));
            }
            return set;
        }
    }
}
