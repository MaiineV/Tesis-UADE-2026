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
    }
}
