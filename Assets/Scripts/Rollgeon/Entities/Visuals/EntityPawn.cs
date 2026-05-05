using System;
using System.Collections;
using System.Collections.Generic;
using Rollgeon.Grid;
using Rollgeon.Movement;
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

        // Default por step — corto para que el movimiento se vea fluido sin frenar
        // el ritmo (≈8 tiles/s a 0.12). Override por arg si querés tunear.
        private const float DefaultSecondsPerStep = 0.12f;

        [SerializeField, Tooltip("Barra de HP world-space. Null en heroes o pawns sin barra.")]
        private WorldSpaceHealthBar _healthBar;

        private Coroutine _moveAnim;

        public WorldSpaceHealthBar HealthBar => _healthBar;

        public Guid EntityGuid { get; private set; }
        public PawnKind Kind { get; private set; }

        /// <summary>Dirección actual del pawn. Default <see cref="Cardinal.South"/> (mira al
        /// jugador en cámara iso).</summary>
        public Cardinal Facing { get; private set; } = Cardinal.South;

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

        /// <summary>
        /// Setea instantáneamente la rotación del pawn a la cardinal dada. Sin lerp —
        /// en pixel art con cámara iso fija, los lerps intermedios pueden generar frames
        /// "borrosos" que rompen la estética (TECHNICAL.md §17.E shader pixel art).
        /// </summary>
        public void SetFacing(Cardinal facing)
        {
            Facing = facing;
            transform.rotation = facing.ToRotation();
        }

        /// <summary>
        /// Conveniencia: deriva la cardinal dominante del vector <paramref name="from"/> →
        /// <paramref name="to"/> y aplica. Si el delta es cero, no hace nada (preserva
        /// el facing previo).
        /// </summary>
        public void FaceCoord(GridCoord from, GridCoord to)
        {
            if (from == to) return;
            SetFacing(CardinalExtensions.FromDelta(from, to, Facing));
        }

        /// <summary>
        /// Anima al pawn caminando casilla-a-casilla por <paramref name="path"/>. Cada step
        /// se mueve via lerp lineal en <paramref name="secondsPerStep"/> y rota el facing
        /// al inicio del segmento.
        /// <para>
        /// Si <paramref name="movement"/> está provisto, antes de cada step revisa si el
        /// próximo tile fue ocupado por otra entidad mientras tanto y recalcula el path
        /// (A*) para rodear el obstáculo. Útil cuando otra entidad se mueve mid-animación.
        /// </para>
        /// <para>
        /// Cancela cualquier animación en curso. En EditMode (sin coroutines), o si el
        /// path tiene menos de 2 nodos, snapea al destino directamente — esto preserva
        /// los tests EditMode existentes que esperan la posición final inmediata.
        /// </para>
        /// </summary>
        public void AnimatePath(
            IGridManager grid,
            IReadOnlyList<GridCoord> path,
            float secondsPerStep = DefaultSecondsPerStep,
            IMovementService movement = null)
        {
            if (grid == null || path == null || path.Count == 0) return;

            if (_moveAnim != null)
            {
                StopCoroutine(_moveAnim);
                _moveAnim = null;
            }

            // Sin coroutines (EditMode) o path trivial → snap al destino y listo.
            if (!Application.isPlaying || path.Count < 2)
            {
                SnapToGrid(grid, path[path.Count - 1]);
                return;
            }

            _moveAnim = StartCoroutine(AnimatePathCoroutine(grid, path, Mathf.Max(0.01f, secondsPerStep), movement));
        }

        private IEnumerator AnimatePathCoroutine(
            IGridManager grid,
            IReadOnlyList<GridCoord> initialPath,
            float secondsPerStep,
            IMovementService movement)
        {
            // Copiamos a List para poder reemplazar el path al recalcular sin tocar el
            // IReadOnlyList del caller. El destino original es path[Count-1] — lo guardamos
            // por si recalculamos varias veces (mantenemos el target).
            var path = new List<GridCoord>(initialPath);
            var destination = path[path.Count - 1];

            int i = 1;
            while (i < path.Count)
            {
                var prev = path[i - 1];
                var next = path[i];

                // Recalc on block: si el próximo tile fue ocupado por otra entidad mientras
                // animábamos, intentar rodear. Si no hay alternativa, abortamos en prev.
                if (movement != null && IsBlockedByOther(grid, next))
                {
                    var rerouted = movement.FindPath(prev, destination);
                    if (rerouted == null || rerouted.Count < 2)
                    {
                        // No hay forma de seguir — paramos acá. La posición lógica en grid
                        // sigue apuntando al destino original (lo setea MovementService.Move),
                        // pero visualmente quedamos atascados; el siguiente OnEntityMoved del
                        // mismo guid resincroniza si hace falta.
                        break;
                    }
                    path = rerouted;
                    i = 1;
                    continue;
                }

                FaceCoord(prev, next);

                Vector3 startPos = transform.position;
                Vector3 endPos = grid.GridToWorld(next);
                if (Kind == PawnKind.Hero) endPos.y += HeroYOffset;

                float elapsed = 0f;
                while (elapsed < secondsPerStep)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / secondsPerStep);
                    transform.position = Vector3.Lerp(startPos, endPos, t);
                    yield return null;
                }
                transform.position = endPos;
                i++;
            }
            _moveAnim = null;
        }

        /// <summary>
        /// True si <paramref name="coord"/> está ocupado por una entidad distinta al pawn
        /// actual. El propio pawn aparece como ocupante en <paramref name="grid"/> mientras
        /// está en su tile, así que filtramos por <see cref="EntityGuid"/>.
        /// </summary>
        private bool IsBlockedByOther(IGridManager grid, GridCoord coord)
        {
            if (!grid.IsOccupied(coord)) return false;
            if (!grid.TryGetOccupant(coord, out var occupant)) return true;
            return occupant != EntityGuid;
        }

        public enum PawnKind { Hero, Enemy, Boss }
    }
}
