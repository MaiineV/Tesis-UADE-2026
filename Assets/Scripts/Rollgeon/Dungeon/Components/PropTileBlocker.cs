using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Dungeon.Components
{
    /// <summary>
    /// Bloquea las celdas de la grilla bajo un prop instanciado en runtime (altar
    /// de encantamientos, pedestales de tienda) registrando un guid por celda en
    /// el <see cref="IGridManager"/> — mismo patrón de "entidad sin turno" que usa
    /// <c>ChestService</c> para los cofres. El footprint sale del AABB XZ de los
    /// renderers del prop (la mesa de encantamientos ocupa 4x5 tiles, no una);
    /// sin renderers cae a la celda bajo el transform. Con las celdas ocupadas,
    /// el BFS de highlight y el A* del click-to-move las excluyen solos.
    /// </summary>
    /// <remarks>
    /// El registro se difiere un frame tras cada <c>OnRoomEntered</c>:
    /// <c>RoomGridLoader.LoadRoom</c> limpia TODA la ocupancia y el orden de
    /// suscripción entre listeners del evento no está garantizado — al frame
    /// siguiente la grilla ya está cargada, cualquiera sea el orden. Además la
    /// limpieza de LoadRoom obliga a re-registrar en cada entrada a la sala (los
    /// props sobreviven entre visitas pero su ocupancia no).
    /// </remarks>
    [AddComponentMenu("Rollgeon/Dungeon/Prop Tile Blocker")]
    public sealed class PropTileBlocker : MonoBehaviour
    {
        // Un guid por celda bloqueada: el GridManager mapea guid↔coord 1:1, así
        // que un footprint multi-celda necesita un registro por celda.
        private readonly List<Guid> _blockedCells = new List<Guid>();
        private Coroutine _pending;

        /// <summary>Attach idempotente por código — los prefabs de props no traen
        /// el componente; los services lo agregan al instanciar.</summary>
        public static PropTileBlocker Attach(GameObject prop)
        {
            if (prop == null) return null;
            var blocker = prop.GetComponent<PropTileBlocker>();
            if (blocker == null) blocker = prop.AddComponent<PropTileBlocker>();
            return blocker;
        }

        private void OnEnable()
        {
            EventManager.Subscribe(EventName.OnRoomEntered, HandleRoomEntered);
            ScheduleRegister();
        }

        private void OnDisable()
        {
            EventManager.UnSubscribe(EventName.OnRoomEntered, HandleRoomEntered);
            if (_pending != null)
            {
                StopCoroutine(_pending);
                _pending = null;
            }
            // Al destruirse el prop (ej. pedestal comprado) la celda vuelve a ser
            // transitable. Si la ocupancia ya fue limpiada por LoadRoom, es no-op.
            TryUnregister();
        }

        private void HandleRoomEntered(params object[] _) => ScheduleRegister();

        private void ScheduleRegister()
        {
            if (!isActiveAndEnabled) return;
            if (_pending != null) StopCoroutine(_pending);
            _pending = StartCoroutine(RegisterNextFrame());
        }

        private IEnumerator RegisterNextFrame()
        {
            yield return null;
            _pending = null;
            TryRegister();
        }

        /// <summary>Registra las celdas del footprint si el prop vive en la sala
        /// activa, salteando celdas no walkables u ocupadas (ej. el jugador parado
        /// encima al entrar). Internal para tests (el diferimiento por coroutine
        /// no corre en EditMode).</summary>
        internal void TryRegister()
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return;
            if (!IsInActiveRoom()) return;

            TryUnregister();
            foreach (var coord in FootprintCells(grid))
            {
                if (!grid.IsWalkable(coord) || grid.IsOccupied(coord)) continue;
                var cellGuid = Guid.NewGuid();
                grid.Register(cellGuid, coord);
                _blockedCells.Add(cellGuid);
            }
        }

        internal void TryUnregister()
        {
            if (_blockedCells.Count == 0) return;
            if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                foreach (var cellGuid in _blockedCells) grid.Unregister(cellGuid);
            }
            _blockedCells.Clear();
        }

        // Celda bloqueada = su centro cae dentro del AABB XZ de los renderers,
        // encogido un margen chico para que un mesh que apenas roza la celda
        // vecina no la anexe (≈ celdas cubiertas al menos a la mitad).
        private IEnumerable<GridCoord> FootprintCells(IGridManager grid)
        {
            if (!TryGetRendererBounds(out var bounds))
            {
                yield return grid.WorldToGrid(transform.position);
                yield break;
            }

            float margin = 0.05f * Mathf.Max(grid.TileSize, 0.01f);
            var min = grid.WorldToGrid(bounds.min);
            var max = grid.WorldToGrid(bounds.max);
            for (int x = min.X; x <= max.X; x++)
            {
                for (int y = min.Y; y <= max.Y; y++)
                {
                    var coord = new GridCoord(x, y);
                    var center = grid.GridToWorld(coord);
                    if (center.x > bounds.min.x + margin && center.x < bounds.max.x - margin
                        && center.z > bounds.min.z + margin && center.z < bounds.max.z - margin)
                    {
                        yield return coord;
                    }
                }
            }
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer == null) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        // La grilla cargada es solo la de la sala activa — un prop de otra sala
        // caería en coords de la sala actual y bloquearía celdas ajenas.
        private bool IsInActiveRoom()
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null)
                return false;
            var roomRoot = dungeon.CurrentRoomInstance?.SpawnedPrefab;
            return roomRoot != null && transform.IsChildOf(roomRoot.transform);
        }
    }
}
