using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Template de sala — prefab + enemy configuration. TECHNICAL.md §13.6.
    /// El layout físico (grid, spawn points, puertas, bounds) vive en el
    /// <see cref="Components.RoomLayout"/> del <see cref="RoomPrefab"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Dungeon/Room", fileName = "Room")]
    public class RoomSO : SerializedScriptableObject
    {
        [Title("Identity")]
        public string RoomId;
        public string DisplayName;
        public RoomType Type;

        [Title("Prefab")]
        [Tooltip("Prefab instanciado en world-space cuando el player entra a la sala. Debe tener un RoomLayout.")]
        public GameObject RoomPrefab;

        [Tooltip("Tamaño de la sala en celdas Isaac (usado por la topología del DungeonManager para detectar vecinos 4-adjacent).")]
        public Vector2Int GridSize = Vector2Int.one;

        [Title("Enemies")]
        [Tooltip("Setups pre-diseñados — al entrar a la sala se elige uno random. Vacío = fallback a EnemyPool ponderado.")]
        public List<EnemySetupSO> PossibleSetups = new List<EnemySetupSO>();

        [Tooltip("Fallback ponderado cuando PossibleSetups está vacío. Se roteá 1:1 contra RoomLayout.EnemySpawnPoints.")]
        public EnemyPoolSO EnemyPool;

        [Title("Floor View")]
        [Tooltip("Sprite opcional dibujado sobre el shell de la sala en el floor view (ej. boss/shop). Null = sin overlay, solo el shell.")]
        [PreviewField(48, ObjectFieldAlignment.Left)]
        public Sprite ShellIcon;
    }
}
