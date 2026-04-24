using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.GameCamera;
using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Floor generator + navegación runtime. TECHNICAL.md §13.6.
    /// Implementa <see cref="IDungeonService"/> y se registra en
    /// <see cref="ServiceScope.Run"/> via <see cref="CreateAndRegister"/>.
    /// <para>
    /// El piso es un grafo de <see cref="RoomInstance"/>s sobre una grilla
    /// Vector2Int (Isaac-style). Una sola sala está instanciada como prefab
    /// en world-space a la vez (la <see cref="CurrentRoomInstance"/>); el
    /// resto existe como nodos + shells procedurales para el floor view.
    /// </para>
    /// </summary>
    public sealed class DungeonManager : IDungeonService, IDisposable
    {
        private const int MinRoomCount = 3;
        private const float DefaultTileSize = 1f;
        private const float MinShellSize = 6f;
        private const float CellSpacing = 0f;

        private static readonly Vector2Int[] CardinalSteps =
        {
            new Vector2Int(0, 1),   // N
            new Vector2Int(0, -1),  // S
            new Vector2Int(1, 0),   // E
            new Vector2Int(-1, 0),  // W
        };

        private readonly Dictionary<Guid, RoomInstance> _instances = new();
        private readonly Dictionary<Guid, FloorShell> _shells = new();
        private readonly Dictionary<Vector2Int, Guid> _cellIndex = new();

        private Guid _currentId = Guid.Empty;
        private RoomSO _runtimeBossRoom;
        private Vector3 _stepSize = new(10f, 0f, 10f);

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onEntityDestroyedHandler;

        public RoomSO CurrentRoom => CurrentRoomInstance?.Template;

        public RoomInstance CurrentRoomInstance =>
            _currentId != Guid.Empty && _instances.TryGetValue(_currentId, out var ri) ? ri : null;

        public DungeonManager()
        {
            _onCombatEndHandler = OnCombatEnd;
            _onEntityDestroyedHandler = OnEntityDestroyed;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnEntityDestroyed, _onEntityDestroyedHandler);
        }

        public void GenerateFloor(FloorLayoutSO layout, int seed)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            ClearState();

            var rng = new System.Random(seed);
            int targetCount = rng.Next(
                Math.Max(layout.RoomCountMin, MinRoomCount),
                Math.Max(layout.RoomCountMax, MinRoomCount) + 1);

            // 1. Topología: random walk en Vector2Int hasta placement de N cells.
            var cells = GenerateTopology(targetCount, rng);

            // 2. Asignar templates por cell.
            var assignments = AssignTemplates(cells, layout, rng);

            // 3. Computar stepSize del piso en world-space.
            _stepSize = ComputeStepSize(assignments);

            // 4. Crear RoomInstance + shell metadata por cell.
            foreach (var cell in cells)
            {
                var template = assignments[cell];
                var id = Guid.NewGuid();
                var worldPos = CellToWorld(cell);
                var initialState = InitialStateFor(template != null ? template.Type : RoomType.Combat);

                var instance = new RoomInstance
                {
                    InstanceId = id,
                    Template = template,
                    WorldPosition = worldPos,
                    GridCell = cell,
                    State = initialState
                };

                _instances[id] = instance;
                _cellIndex[cell] = id;
                _shells[id] = new FloorShell
                {
                    InstanceId = id,
                    WorldPosition = worldPos,
                    Size = ShellSizeFor(template)
                };
            }

            // 5. Wire connections 4-adjacent.
            foreach (var (cell, id) in _cellIndex)
            {
                var instance = _instances[id];
                foreach (var dir in AllDirections())
                {
                    var step = StepFor(dir);
                    var neighborCell = cell + step;
                    if (_cellIndex.TryGetValue(neighborCell, out var neighborId))
                    {
                        instance.Connections[dir] = neighborId;
                    }
                }
            }

            // 6. Seed default DoorStates en cada instancia.
            foreach (var instance in _instances.Values)
            {
                foreach (var dir in AllDirections())
                {
                    if (!instance.Connections.ContainsKey(dir)) continue;
                    var key = DoorStateKey(dir);
                    if (!instance.ObjectStates.ContainsKey(key))
                    {
                        instance.ObjectStates.Set(key, new DoorState
                        {
                            SpawnPointId = key,
                            Direction = dir,
                            Forced = false,
                            Unlocked = false
                        });
                    }
                }
            }

            // 7. Instanciar todas las rooms.
            foreach (var instance in _instances.Values)
                InstantiateRoomPrefab(instance);

            // 8. Seed _currentId con la start room.
            var startId = FindStartInstanceId(cells);
            _currentId = startId;

            EventManager.Trigger(EventName.OnRoomEntered, startId,
                CurrentRoom != null ? CurrentRoom.RoomId : string.Empty);
        }

        public IReadOnlyDictionary<Guid, RoomInstance> GetAllRoomInstances() => _instances;

        public IReadOnlyDictionary<Guid, FloorShell> GetFloorShells() => _shells;

        public bool CanEnterRoomByDoor(DoorDirection direction, out Guid neighborInstanceId)
        {
            neighborInstanceId = Guid.Empty;
            var current = CurrentRoomInstance;
            if (current == null) return false;
            if (!current.Connections.TryGetValue(direction, out neighborInstanceId)) return false;

            // Sala cleared → libre.
            if (current.State == RoomState.Cleared) return true;

            // Uncleared + forced door → libre.
            if (current.ObjectStates.TryGet<DoorState>(DoorStateKey(direction), out var doorState)
                && (doorState.Forced || doorState.Unlocked))
            {
                return true;
            }

            // Uncleared + combat activo → locked.
            return false;
        }

        public bool EnterRoomByDoor(DoorDirection direction)
        {
            if (!CanEnterRoomByDoor(direction, out var neighborId)) return false;
            return TransitionTo(neighborId);
        }

        public bool EnterRoomByInstanceId(Guid instanceId)
        {
            if (!_instances.ContainsKey(instanceId)) return false;
            return TransitionTo(instanceId);
        }

        public Bounds GetFloorBounds()
        {
            if (_shells.Count == 0) return default;

            Bounds combined = default;
            bool initialized = false;
            foreach (var shell in _shells.Values)
            {
                var b = new Bounds(shell.WorldPosition, shell.Size);
                if (!initialized)
                {
                    combined = b;
                    initialized = true;
                }
                else
                {
                    combined.Encapsulate(b);
                }
            }
            return combined;
        }

        public IReadOnlyList<WallOccluder> GetCurrentRoomOccluders()
        {
            var current = CurrentRoomInstance;
            if (current?.SpawnedPrefab == null) return Array.Empty<WallOccluder>();
            return current.SpawnedPrefab.GetComponentsInChildren<WallOccluder>(includeInactive: true);
        }

        public void Dispose()
        {
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onEntityDestroyedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnEntityDestroyed, _onEntityDestroyedHandler);
                _onEntityDestroyedHandler = null;
            }

            ClearState();
        }

        /// <summary>
        /// Factory: crea un <see cref="DungeonManager"/>, genera el piso, y lo
        /// registra como <see cref="IDungeonService"/> en <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static DungeonManager CreateAndRegister(FloorLayoutSO layout, int seed)
        {
            var manager = new DungeonManager();
            manager.GenerateFloor(layout, seed);
            ServiceLocator.AddService<IDungeonService>(manager, ServiceScope.Run);
            return manager;
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        private bool TransitionTo(Guid neighborId)
        {
            if (!_instances.TryGetValue(neighborId, out var neighbor)) return false;

            DeactivateCurrentRoomCombat();

            _currentId = neighborId;

            EventManager.Trigger(EventName.OnRoomEntered, _currentId,
                CurrentRoom != null ? CurrentRoom.RoomId : string.Empty);
            return true;
        }

        private void DeactivateCurrentRoomCombat()
        {
            var current = CurrentRoomInstance;
            if (current == null) return;
            current.SpawnedEnemies.Clear();
        }

        private void InstantiateRoomPrefab(RoomInstance instance)
        {
            if (instance?.Template == null) return;

            var prefab = instance.Template.RoomPrefab;
            if (prefab == null)
            {
                // Tests legacy + samples sin prefab autoreado — nada que instanciar.
                return;
            }

            instance.SpawnedPrefab = UnityEngine.Object.Instantiate(
                prefab, instance.WorldPosition, Quaternion.identity);

            ConfigureDoorSlots(instance);
        }

        /// <summary>
        /// Por cada <see cref="DoorSlotRef"/> del prefab: si hay vecino en esa
        /// dirección, activa la puerta (wallPlug off) y le cablea el
        /// <see cref="DoorController"/> con <see cref="RoomInstance.InstanceId"/>
        /// + dirección; si no, activa el wallPlug. El estado inicial (Open vs
        /// LockedCombat) lo resuelve <see cref="SyncDoorVisualStates"/>.
        /// </summary>
        private void ConfigureDoorSlots(RoomInstance instance)
        {
            if (instance?.SpawnedPrefab == null) return;

            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null) return;

            foreach (var slot in layout.DoorSlots)
            {
                if (slot == null) continue;

                bool connected = instance.Connections.ContainsKey(slot.Direction);

                if (slot.WallPlug != null) slot.WallPlug.SetActive(!connected);
                if (slot.DoorRoot != null) slot.DoorRoot.SetActive(connected);

                if (!connected) continue;
                if (slot.DoorRoot == null) continue;

                var controller = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                if (controller == null) continue;

                controller.OwnerRoomInstanceId = instance.InstanceId;
                controller.Direction = slot.Direction;
                controller.SpawnPointId = DoorStateKey(slot.Direction);
            }

            SyncDoorVisualStates(instance);
        }

        /// <summary>
        /// Recalcula el estado visual de cada puerta conectada de
        /// <paramref name="instance"/> a partir de la <see cref="RoomState"/>
        /// de la sala y el <see cref="DoorState"/> persistido.
        /// </summary>
        private void SyncDoorVisualStates(RoomInstance instance)
        {
            if (instance?.SpawnedPrefab == null) return;
            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null) return;

            foreach (var slot in layout.DoorSlots)
            {
                if (slot == null || slot.DoorRoot == null) continue;
                var controller = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                if (controller == null) continue;

                if (!instance.Connections.ContainsKey(slot.Direction))
                {
                    controller.SetState(DoorVisualState.Tapiada);
                    continue;
                }

                instance.ObjectStates.TryGet<DoorState>(DoorStateKey(slot.Direction), out var doorState);
                bool forced = doorState != null && (doorState.Forced || doorState.Unlocked);

                if (instance.State == RoomState.Cleared || forced)
                {
                    controller.SetState(DoorVisualState.Open);
                }
                else
                {
                    controller.SetState(DoorVisualState.LockedCombat);
                }
            }
        }

        private void OnCombatEnd(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (args[0] is not Guid roomInstanceId) return;
            if (args[1] is not CombatOutcome outcome) return;
            if (outcome != CombatOutcome.Victory) return;

            if (!_instances.TryGetValue(roomInstanceId, out var instance)) return;
            if (instance.State == RoomState.Cleared) return;

            instance.State = RoomState.Cleared;

            // Unlock todas las puertas conectadas + refrescar visual de la
            // sala actualmente instanciada (solo si coincide con la clareada).
            foreach (var dir in AllDirections())
            {
                if (!instance.Connections.ContainsKey(dir)) continue;
                var key = DoorStateKey(dir);
                if (!instance.ObjectStates.TryGet<DoorState>(key, out var doorState)) continue;
                doorState.Unlocked = true;
            }

            if (_currentId == instance.InstanceId)
            {
                SyncDoorVisualStates(instance);
            }

            EventManager.Trigger(EventName.OnRoomCleared, roomInstanceId);

            // Piso completo = boss clareada. Trigger OnFloorCleared para que
            // la VictoryScreen (§UI) reaccione. floorIndex=0 placeholder hasta
            // que exista piso multi-floor.
            if (instance.Template != null && instance.Template.Type == RoomType.Boss)
            {
                EventManager.Trigger(EventName.OnFloorCleared, roomInstanceId, 0);
            }
        }

        /// <summary>
        /// Hook global de muertes — cuando un enemigo registrado por el resolver
        /// en <see cref="RoomInstance.SpawnedEnemies"/> cae, actualiza su
        /// <see cref="EnemySpawnState.IsDead"/> y, si todos los enemigos de la
        /// sala actual murieron, dispara <see cref="EventName.OnCombatEnd"/>
        /// con <see cref="CombatOutcome.Victory"/>.
        /// </summary>
        private void OnEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (args[0] is not Guid destroyedId) return;

            foreach (var instance in _instances.Values)
            {
                int idx = instance.SpawnedEnemies.IndexOf(destroyedId);
                if (idx < 0) continue;

                instance.SpawnedEnemies.RemoveAt(idx);

                // Match contra EnemySpawnState correspondiente → mark IsDead.
                foreach (var kv in instance.ObjectStates.Enumerate())
                {
                    if (kv.Value is EnemySpawnState es && !es.IsDead
                        && es.SpawnPointIndex == idx)
                    {
                        es.IsDead = true;
                        break;
                    }
                }

                // Si la sala activa queda sin enemigos → Victory.
                if (instance.InstanceId == _currentId
                    && instance.State == RoomState.Uncleared
                    && instance.SpawnedEnemies.Count == 0)
                {
                    EventManager.Trigger(EventName.OnCombatEnd,
                        instance.InstanceId, CombatOutcome.Victory);
                }
                return;
            }
        }

        private void ClearState()
        {
            foreach (var instance in _instances.Values)
            {
                if (instance.SpawnedPrefab != null)
                {
                    UnityEngine.Object.Destroy(instance.SpawnedPrefab);
                    instance.SpawnedPrefab = null;
                }
                instance.SpawnedEnemies.Clear();
            }

            if (_runtimeBossRoom != null)
            {
                UnityEngine.Object.DestroyImmediate(_runtimeBossRoom);
                _runtimeBossRoom = null;
            }

            _instances.Clear();
            _shells.Clear();
            _cellIndex.Clear();
            _currentId = Guid.Empty;
        }

        // -----------------------------------------------------------------
        // Topology generation
        // -----------------------------------------------------------------

        private List<Vector2Int> GenerateTopology(int targetCount, System.Random rng)
        {
            var cells = new List<Vector2Int> { Vector2Int.zero };
            var frontier = new HashSet<Vector2Int> { Vector2Int.zero };
            var used = new HashSet<Vector2Int> { Vector2Int.zero };

            while (cells.Count < targetCount && frontier.Count > 0)
            {
                // Pick una cell de la frontera al azar; extender a un vecino libre.
                Vector2Int seed;
                {
                    int pick = rng.Next(frontier.Count);
                    int idx = 0;
                    seed = Vector2Int.zero;
                    foreach (var f in frontier)
                    {
                        if (idx++ == pick) { seed = f; break; }
                    }
                }

                var candidates = new List<Vector2Int>(4);
                foreach (var step in CardinalSteps)
                {
                    var c = seed + step;
                    if (!used.Contains(c)) candidates.Add(c);
                }

                if (candidates.Count == 0)
                {
                    frontier.Remove(seed);
                    continue;
                }

                var next = candidates[rng.Next(candidates.Count)];
                cells.Add(next);
                used.Add(next);
                frontier.Add(next);
            }

            return cells;
        }

        private Dictionary<Vector2Int, RoomSO> AssignTemplates(
            List<Vector2Int> cells, FloorLayoutSO layout, System.Random rng)
        {
            var assignments = new Dictionary<Vector2Int, RoomSO>(cells.Count);

            // Start en (0,0) — template explícito si existe.
            var startCell = Vector2Int.zero;

            // Boss en la cell de mayor Manhattan distance.
            Vector2Int bossCell = startCell;
            int bossDist = -1;
            foreach (var c in cells)
            {
                int d = Math.Abs(c.x - startCell.x) + Math.Abs(c.y - startCell.y);
                if (d > bossDist)
                {
                    bossDist = d;
                    bossCell = c;
                }
            }

            // Shop + potion en cells intermedias elegidas al azar.
            var intermediate = new List<Vector2Int>(cells.Count);
            foreach (var c in cells)
            {
                if (c == startCell || c == bossCell) continue;
                intermediate.Add(c);
            }

            Vector2Int? shopCell = null;
            Vector2Int? potionCell = null;

            if (layout.ShopRooms != null && layout.ShopRooms.Count > 0 && intermediate.Count > 0)
            {
                int idx = rng.Next(intermediate.Count);
                shopCell = intermediate[idx];
                intermediate.RemoveAt(idx);
            }

            if (layout.PotionRooms != null && layout.PotionRooms.Count > 0 && intermediate.Count > 0)
            {
                int idx = rng.Next(intermediate.Count);
                potionCell = intermediate[idx];
                intermediate.RemoveAt(idx);
            }

            foreach (var cell in cells)
            {
                RoomSO template;
                if (cell == startCell)
                {
                    template = layout.StartRoom
                               ?? PickRandom(layout.CombatRooms, rng);
                }
                else if (cell == bossCell)
                {
                    template = layout.DefaultBossRoomTemplate ?? BuildRuntimeBossRoom(layout, rng);
                }
                else if (shopCell.HasValue && cell == shopCell.Value)
                {
                    template = PickRandom(layout.ShopRooms, rng);
                }
                else if (potionCell.HasValue && cell == potionCell.Value)
                {
                    template = PickRandom(layout.PotionRooms, rng);
                }
                else
                {
                    template = PickRandom(layout.CombatRooms, rng);
                }

                assignments[cell] = template;
            }

            return assignments;
        }

        private Guid FindStartInstanceId(List<Vector2Int> cells)
        {
            // El start siempre es Vector2Int.zero (ver AssignTemplates).
            return _cellIndex[Vector2Int.zero];
        }

        private static T PickRandom<T>(IList<T> list, System.Random rng) where T : class
        {
            if (list == null || list.Count == 0) return null;
            return list[rng.Next(list.Count)];
        }

        private RoomSO BuildRuntimeBossRoom(FloorLayoutSO layout, System.Random rng)
        {
            // LEGACY path: si no hay DefaultBossRoomTemplate, se arma uno runtime
            // a partir del primer BossCandidate. Se elimina cuando todos los
            // FloorLayouts migren a DefaultBossRoomTemplate.
            if (layout.BossCandidates == null || layout.BossCandidates.Count == 0) return null;

            var bossEnemy = layout.BossCandidates[rng.Next(layout.BossCandidates.Count)];
            var bossRoom = ScriptableObject.CreateInstance<RoomSO>();
            bossRoom.RoomId = $"boss_{bossEnemy.name}";
            bossRoom.DisplayName = bossEnemy.name;
            bossRoom.Type = RoomType.Boss;
            _runtimeBossRoom = bossRoom;
            return bossRoom;
        }

        private static RoomState InitialStateFor(RoomType type)
        {
            return type switch
            {
                RoomType.Combat => RoomState.Uncleared,
                RoomType.Boss   => RoomState.Uncleared,
                _               => RoomState.Cleared,
            };
        }

        // -----------------------------------------------------------------
        // Geometry helpers
        // -----------------------------------------------------------------

        private Vector3 ComputeStepSize(Dictionary<Vector2Int, RoomSO> assignments)
        {
            float maxX = MinShellSize;
            float maxZ = MinShellSize;

            foreach (var template in assignments.Values)
            {
                if (template == null) continue;
                var size = TemplateBoundsSize(template);
                if (size.x > maxX) maxX = size.x;
                if (size.z > maxZ) maxZ = size.z;
            }

            return new Vector3(maxX + CellSpacing, 0f, maxZ + CellSpacing);
        }

        private Vector3 CellToWorld(Vector2Int cell) =>
            new(cell.x * _stepSize.x, 0f, cell.y * _stepSize.z);

        private Vector3 ShellSizeFor(RoomSO template)
        {
            if (template == null) return new Vector3(MinShellSize, 1f, MinShellSize);
            var size = TemplateBoundsSize(template);
            return new Vector3(
                Mathf.Max(size.x, MinShellSize),
                Mathf.Max(size.y, 1f),
                Mathf.Max(size.z, MinShellSize));
        }

        private static Vector3 TemplateBoundsSize(RoomSO template)
        {
            if (template?.RoomPrefab != null)
            {
                var layout = template.RoomPrefab.GetComponent<RoomLayout>();
                if (layout != null && layout.LocalBounds.size != Vector3.zero)
                {
                    return layout.LocalBounds.size;
                }
            }

            // Fallback: un cubo acorde a GridSize * DefaultTileSize.
            float w = Mathf.Max(1, template != null ? template.GridSize.x : 1) * DefaultTileSize;
            float d = Mathf.Max(1, template != null ? template.GridSize.y : 1) * DefaultTileSize;
            return new Vector3(Mathf.Max(w, MinShellSize), 1f, Mathf.Max(d, MinShellSize));
        }

        // -----------------------------------------------------------------
        // Direction helpers
        // -----------------------------------------------------------------

        private static Vector2Int StepFor(DoorDirection dir) => dir switch
        {
            DoorDirection.North => new Vector2Int(0, 1),
            DoorDirection.South => new Vector2Int(0, -1),
            DoorDirection.East  => new Vector2Int(1, 0),
            DoorDirection.West  => new Vector2Int(-1, 0),
            _                   => Vector2Int.zero,
        };

        private static IEnumerable<DoorDirection> AllDirections()
        {
            yield return DoorDirection.North;
            yield return DoorDirection.South;
            yield return DoorDirection.East;
            yield return DoorDirection.West;
        }

        private static string DoorStateKey(DoorDirection dir) => dir switch
        {
            DoorDirection.North => "door_N",
            DoorDirection.South => "door_S",
            DoorDirection.East  => "door_E",
            DoorDirection.West  => "door_W",
            _                   => "door_?",
        };
    }
}
