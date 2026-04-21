using System.Collections.Generic;
using Rollgeon.Entities;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Floor Layout", fileName = "FloorLayout")]
    public class FloorLayoutSO : SerializedScriptableObject
    {
        [Title("Room Count")]
        [MinValue(1)] public int RoomCountMin = 8;
        [MinValue(1)] public int RoomCountMax = 14;

        [Title("Room Pools")]
        public List<RoomSO> CombatRooms = new();

        [Title("Special Rooms")]
        public List<RoomSO> ShopRooms = new();
        public List<RoomSO> PotionRooms = new();

        [Title("Boss")]
        public List<EnemyDataSO> BossCandidates = new();

        [Title("Start")]
        [InfoBox("Room de entrada (RoomType.Start). Va en posición 0 del piso. " +
                 "Si es null, la primera room es la primera Combat (legacy).")]
        public RoomSO StartRoom;
    }
}
