using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Marker MonoBehaviour opcional para spawn points singulares no cubiertos
    /// por las listas tipadas de <see cref="RoomLayout"/> (player / enemies /
    /// rewards / obstáculos). TECHNICAL.md §13.3.
    /// </summary>
    /// <remarks>
    /// En el pipeline §13.6 los spawn points principales son Transforms en
    /// <see cref="RoomLayout"/>. Este componente queda para marcadores custom
    /// (NPCs, props) donde el diseñador quiera una resolución tile-grid
    /// explícita.
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
