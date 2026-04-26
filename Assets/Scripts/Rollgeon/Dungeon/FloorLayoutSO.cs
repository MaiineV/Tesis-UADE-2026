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
        [InfoBox("ShopRooms es obligatorio — TECHNICAL.md §17.F dice 1 shop por piso siempre. " +
                 "El DungeonManager logea error si la lista queda vacía.")]
        [Required("Asignar al menos un RoomSO de tipo Shop — 1 shop por piso es invariante (§17.F).")]
        public List<RoomSO> ShopRooms = new();
        public List<RoomSO> PotionRooms = new();

        [Title("Boss")]
        [Tooltip("Template de la sala boss (prefab + setups). El DungeonManager lo coloca en la cell a mayor distancia Manhattan del start.")]
        public RoomSO DefaultBossRoomTemplate;

        [Tooltip("LEGACY: enemigos candidatos a boss cuando la sala boss se arma runtime. Reemplazado por RoomSO.PossibleSetups dentro de DefaultBossRoomTemplate.")]
        public List<EnemyDataSO> BossCandidates = new();

        [Title("Start")]
        [InfoBox("Room de entrada (RoomType.Start). Va en posición 0 del piso. " +
                 "Si es null, la primera room es la primera Combat (legacy).")]
        public RoomSO StartRoom;
    }
}
