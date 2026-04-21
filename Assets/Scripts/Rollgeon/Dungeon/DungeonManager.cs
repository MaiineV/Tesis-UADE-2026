using System;
using System.Collections.Generic;
using Patterns;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Manages floor generation and room navigation for a dungeon run.
    /// Implements <see cref="IDungeonService"/> and registers itself in
    /// <see cref="ServiceScope.Run"/> via <see cref="CreateAndRegister"/>.
    /// </summary>
    public sealed class DungeonManager : IDungeonService, IDisposable
    {
        private readonly List<RoomSO> _rooms = new();
        private int _currentIndex = -1;
        private RoomSO _runtimeBossRoom;

        private const int MinRoomCount = 3;

        public RoomSO CurrentRoom =>
            _currentIndex >= 0 && _currentIndex < _rooms.Count ? _rooms[_currentIndex] : null;

        public int CurrentRoomIndex => _currentIndex;
        public int RoomCount => _rooms.Count;
        public bool IsLastRoom => _currentIndex >= 0 && _currentIndex == _rooms.Count - 1;

        public void GenerateFloor(FloorLayoutSO layout, int seed)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            ClearState();

            var rng = new System.Random(seed);

            int targetCount = rng.Next(
                Math.Max(layout.RoomCountMin, MinRoomCount),
                Math.Max(layout.RoomCountMax, MinRoomCount) + 1);

            // Reserve last slot for boss — fill combat rooms into the rest
            int combatSlots = targetCount - 1; // boss takes the last slot

            // Fill combat rooms
            if (layout.CombatRooms != null && layout.CombatRooms.Count > 0)
            {
                for (int i = 0; i < combatSlots; i++)
                {
                    int idx = rng.Next(layout.CombatRooms.Count);
                    _rooms.Add(layout.CombatRooms[idx]);
                }
            }

            // Insert shop room at a random middle position
            if (layout.ShopRooms != null && layout.ShopRooms.Count > 0)
            {
                int shopIdx = rng.Next(layout.ShopRooms.Count);
                int insertPos = GetRandomMiddlePosition(rng, _rooms.Count);
                _rooms.Insert(insertPos, layout.ShopRooms[shopIdx]);
            }

            // Insert potion room at a random middle position
            if (layout.PotionRooms != null && layout.PotionRooms.Count > 0)
            {
                int potionIdx = rng.Next(layout.PotionRooms.Count);
                int insertPos = GetRandomMiddlePosition(rng, _rooms.Count);
                _rooms.Insert(insertPos, layout.PotionRooms[potionIdx]);
            }

            // Last slot: runtime boss room
            if (layout.BossCandidates != null && layout.BossCandidates.Count > 0)
            {
                int bossIdx = rng.Next(layout.BossCandidates.Count);
                var bossEnemy = layout.BossCandidates[bossIdx];

                var bossRoom = ScriptableObject.CreateInstance<RoomSO>();
                bossRoom.RoomId = $"boss_{bossEnemy.name}";
                bossRoom.DisplayName = bossEnemy.name;
                bossRoom.Type = RoomType.Boss;
                _runtimeBossRoom = bossRoom;
                _rooms.Add(bossRoom);
            }

            // Start room en índice 0 (si está configurado). Se inserta al final
            // del fill para no invalidar los cálculos de combatSlots ni las
            // middle positions de shop/potion.
            if (layout.StartRoom != null)
            {
                _rooms.Insert(0, layout.StartRoom);
            }

            _currentIndex = 0;

            EventManager.Trigger(EventName.OnRoomEntered,
                Guid.NewGuid(), CurrentRoom != null ? CurrentRoom.RoomId : string.Empty);
        }

        public bool NextRoom()
        {
            if (IsLastRoom)
            {
                EventManager.Trigger(EventName.OnFloorCleared, Guid.NewGuid(), _currentIndex);
                return false;
            }

            _currentIndex++;

            EventManager.Trigger(EventName.OnRoomEntered,
                Guid.NewGuid(), CurrentRoom != null ? CurrentRoom.RoomId : string.Empty);

            return true;
        }

        public IReadOnlyList<RoomSO> GetFloorRooms()
        {
            return _rooms.AsReadOnly();
        }

        public void Dispose()
        {
            ClearState();
        }

        /// <summary>
        /// Factory: creates a <see cref="DungeonManager"/>, generates the floor,
        /// and registers it as <see cref="IDungeonService"/> in <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static DungeonManager CreateAndRegister(FloorLayoutSO layout, int seed)
        {
            var manager = new DungeonManager();
            manager.GenerateFloor(layout, seed);
            ServiceLocator.AddService<IDungeonService>(manager, ServiceScope.Run);
            return manager;
        }

        private void ClearState()
        {
            if (_runtimeBossRoom != null)
            {
                UnityEngine.Object.DestroyImmediate(_runtimeBossRoom);
                _runtimeBossRoom = null;
            }

            _rooms.Clear();
            _currentIndex = -1;
        }

        /// <summary>
        /// Returns a random index in the "middle" of the list (excluding first and last positions)
        /// to place special rooms between combat encounters.
        /// </summary>
        private static int GetRandomMiddlePosition(System.Random rng, int listCount)
        {
            if (listCount <= 1) return 0;
            // Middle range: [1, listCount - 1) — avoids first and last positions
            int min = 1;
            int max = Math.Max(min + 1, listCount);
            return rng.Next(min, max);
        }
    }
}
