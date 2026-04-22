using System.Collections.Generic;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Room", fileName = "Room")]
    public class RoomSO : SerializedScriptableObject
    {
        [Title("Identity")]
        public string RoomId;
        public string DisplayName;
        public RoomType Type;

        [Title("Enemies")]
        public EnemyPoolSO EnemyPool;

        [Title("Grid Layout")]
        [Tooltip("Snapshot de tiles walkable/blocked. Si IsEmpty, el GridManager trata la sala como rectángulo sin obstáculos.")]
        public GridSnapshot GridLayout;

        [Tooltip("Tile donde aparece el hero cuando se entra a esta sala.")]
        public GridCoord PlayerSpawn = GridCoord.Zero;

        [Tooltip("Tiles donde aparecen los enemigos (asignados en orden, roteando si faltan).")]
        public List<GridCoord> EnemySpawnPoints = new List<GridCoord>();
    }
}
