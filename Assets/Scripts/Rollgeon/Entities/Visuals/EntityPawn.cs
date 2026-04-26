using System;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// MonoBehaviour que vive en el prefab del GameObject de una entidad
    /// (hero / enemy). Expone el Guid lógico y una API para que el
    /// <see cref="EntityVisualService"/> actualice su posición al moverse en grilla.
    /// </summary>
    /// <remarks>
    /// Placeholder: el FP usa primitives coloreados — más adelante la capa de art
    /// reemplaza prefabs sin cambiar el contrato. El pawn también puede referenciar
    /// una barra de HP y un animator, pero no se requieren para FP.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Entities/Entity Pawn")]
    public sealed class EntityPawn : MonoBehaviour
    {
        // El hero queda elevado sobre el tile para que el modelo no clipée con
        // el piso/grid — los enemies se quedan al ras (Y=0).
        private const float HeroYOffset = 1.4f;

        [SerializeField, Tooltip("Barra de HP world-space. Null en heroes o pawns sin barra.")]
        private WorldSpaceHealthBar _healthBar;

        public WorldSpaceHealthBar HealthBar => _healthBar;

        public Guid EntityGuid { get; private set; }
        public PawnKind Kind { get; private set; }

        public void Bind(Guid guid, PawnKind kind)
        {
            EntityGuid = guid;
            Kind = kind;
            gameObject.name = $"{kind}_{guid.ToString().Substring(0, 8)}";
        }

        public void SetWorldPosition(Vector3 world)
        {
            transform.position = world;
        }

        public void SnapToGrid(IGridManager grid, GridCoord coord)
        {
            if (grid == null) return;
            var pos = grid.GridToWorld(coord);
            if (Kind == PawnKind.Hero) pos.y += HeroYOffset;
            transform.position = pos;
        }

        public enum PawnKind { Hero, Enemy, Boss }
    }
}
