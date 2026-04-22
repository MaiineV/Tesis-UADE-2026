using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Marker MonoBehaviour opcional para salas autoradas como prefab. TECHNICAL.md §13.3.
    /// </summary>
    /// <remarks>
    /// El flujo actual de FP usa <see cref="Rollgeon.Dungeon.RoomSO"/> con
    /// <c>PlayerSpawn</c> + <c>EnemySpawnPoints</c> embebidos. Este componente existe
    /// para el pipeline futuro (§13.3) donde cada sala es un prefab completo en escena;
    /// un editor tool transcribirá la posición world-space a <see cref="GridCoord"/>.
    /// </remarks>
    public sealed class SpawnPoint : MonoBehaviour
    {
        public SpawnKind Kind = SpawnKind.Enemy;

        [Tooltip("Coordenada de grilla resuelta. Si Coord == (0,0) el editor tool debería rellenarla automáticamente.")]
        public GridCoord Coord = GridCoord.Zero;

        public enum SpawnKind
        {
            Player,
            Enemy,
            NPC,
            Prop
        }
    }
}
