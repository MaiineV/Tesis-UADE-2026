using System;
using System.Collections.Generic;
using Patterns;
using Patterns.Save;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Entities.Visuals;
using Rollgeon.GameCamera;
using Rollgeon.Grid;
using Rollgeon.Player;
using Rollgeon.Upgrades.Character;
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
    public sealed class DungeonManager : IDungeonService, ISaveable, IDisposable
    {
        /// <summary>SaveKey del estado espacial del piso (Feature#0028).</summary>
        public const string SaveKeyConst = "run.dungeon_state";

        private const float DefaultTileSize = 1f;
        private const float MinShellSize = 6f;
        private const float CellSpacing = 0f;

        private readonly Dictionary<Guid, RoomInstance> _instances = new();
        private readonly Dictionary<Guid, FloorShell> _shells = new();
        private readonly Dictionary<Vector2Int, Guid> _cellIndex = new();

        private Guid _currentId = Guid.Empty;
        private DoorDirection? _lastEntryDirection;
        private Vector3 _stepSize = new(10f, 0f, 10f);

        /// <summary>
        /// Snapshot cacheado por el auto-restore de <see cref="SaveSystem.Register"/> en
        /// un resume. No se aplica en el acto (la topología no existe todavía cuando el
        /// Register corre en <c>RunController.OnRunStart</c>): lo consume
        /// <see cref="ResumeFromSave"/> tras la generación. (Feature#0028)
        /// </summary>
        private DungeonSnapshot _pendingRestore;

        // Últimos guid/tile válidos del player — se reusan si el capture corre con el player
        // ya limpiado (EndRun llama ClearPlayer antes del capture de OnRunEnd, #0028).
        private string _lastPlayerGuid;
        private GridCoord _lastPlayerCoord;
        private bool _hasLastPlayer;

        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onEntityDestroyedHandler;

        public RoomSO CurrentRoom => CurrentRoomInstance?.Template;

        public RoomInstance CurrentRoomInstance =>
            _currentId != Guid.Empty && _instances.TryGetValue(_currentId, out var ri) ? ri : null;

        public int CurrentFloorSeed { get; private set; }

        public DoorDirection? LastEntryDirection => _lastEntryDirection;

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

            // 1+2. Plan puro: cells + asignaciones (sin side effects).
            var plan = FloorTopologyPlanner.Generate(layout, seed);
            GenerateFromPlan(plan);
        }

        /// <summary>
        /// Genera el piso desde un <see cref="FloorTopologyPlanner.Plan"/> ya resuelto —
        /// mismo pipeline que <see cref="GenerateFloor"/> pero sin pasar por la topología
        /// aleatoria. Permite pisos autorados (tutorial, pisos fijos) siempre que el plan
        /// tenga la Start room en <see cref="Vector2Int.zero"/>.
        /// </summary>
        public void GenerateFromPlan(FloorTopologyPlanner.Plan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            ClearState();
            CurrentFloorSeed = plan.Seed;
            _lastEntryDirection = null;

            if (plan.Warnings != null)
            {
                foreach (var w in plan.Warnings)
                    Debug.LogWarning($"[DungeonManager] {w}");
            }

            var cells = plan.Cells;
            var assignments = plan.Assignments;

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
                    Size = ShellSizeFor(template),
                    Icon = template != null ? template.ShellIcon : null
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

            // 5b. Boss room = dead-end: exactamente 1 entrada (#158). La puerta opuesta
            //     a esa entrada se designa como salida de piso dinámica en ConfigureDoorSlots.
            EnforceBossSingleEntrance();

            // 5c. Guard de puertas (PUL-011): podar conexiones cuya dirección no tiene puerta
            //     autorada en el prefab (o el vecino no tiene la recíproca). Sin esto el minimapa
            //     muestra la conexión pero no hay paso — sala inalcanzable. Corre ANTES del seed
            //     de DoorStates (6) e instanciación (7) para que todo downstream vea el grafo ya
            //     coherente. Con las 21 salas actuales (4 puertas c/u) es no-op.
            PruneDoorlessConnections();

            // 6. Seed default DoorStates en cada instancia.
            foreach (var instance in _instances.Values)
            {
                foreach (var dir in AllDirections())
                {
                    if (!instance.Connections.ContainsKey(dir)) continue;
                    var key = dir.DoorStateKey();
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

            // 7b. Auditar reciprocidad de DoorSlotRefs entre vecinos — sirve
            //     para detectar prefabs con slots N/S/E/W faltantes que dejan
            //     rooms "aisladas" pese a tener Connections válidas.
            AuditDoorSlotReciprocity();

            // 8. Seed _currentId con la start room.
            var startId = FindStartInstanceId();
            _currentId = startId;
            MarkVisited(startId);

            // 8b. Fog of war — solo la sala actual queda visible.
            RefreshRoomVisibility();

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

            // Vecino Locked (gate externo — tutorial, boss key, evento) → bloqueado
            // SIEMPRE, incluso con DoorState.Forced/Unlocked: un force previo no puede
            // abrir una sala gateada después (EffForceDoor marca Forced antes de cruzar).
            if (_instances.TryGetValue(neighborInstanceId, out var lockedNeighbor)
                && lockedNeighbor.State == RoomState.Locked)
            {
                Debug.LogWarning($"[DungeonManager] CanEnterRoomByDoor({direction}) bloqueado — vecino '{neighborInstanceId:N}' state=Locked.");
                return false;
            }

            // Sala cleared → libre.
            if (current.State == RoomState.Cleared) return true;

            // Uncleared + forced door → libre.
            if (current.ObjectStates.TryGet<DoorState>(direction.DoorStateKey(), out var doorState)
                && (doorState.Forced || doorState.Unlocked))
            {
                return true;
            }

            // Uncleared + combat activo → locked.
            Debug.LogWarning($"[DungeonManager] CanEnterRoomByDoor({direction}) bloqueado — sala '{current.InstanceId:N}' state={current.State}.");
            return false;
        }

        public bool EnterRoomByDoor(DoorDirection direction)
        {
            if (!CanEnterRoomByDoor(direction, out var neighborId)) return false;
            _lastEntryDirection = direction.Opposite();
            return TransitionTo(neighborId);
        }

        public bool EnterRoomByInstanceId(Guid instanceId)
        {
            if (!_instances.ContainsKey(instanceId)) return false;
            _lastEntryDirection = null;
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

            // Captura el estado final al cache y suelta la registration — sin esto la
            // próxima run registraría un 2do DungeonManager con la misma SaveKey (#0028).
            SaveSystem.Unregister(this);

            ClearState();
        }

        /// <summary>
        /// Factory: crea un <see cref="DungeonManager"/>, genera el piso, y lo
        /// registra como <see cref="IDungeonService"/> en <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static DungeonManager CreateAndRegister(FloorLayoutSO layout, int seed)
        {
            var manager = new DungeonManager();
            ServiceLocator.AddService<IDungeonService>(manager, ServiceScope.Run);
            manager.GenerateFloor(layout, seed);
            return manager;
        }

        /// <summary>
        /// Factory para pisos autorados (tutorial): igual que <see cref="CreateAndRegister"/>
        /// pero generando desde un plan fijo en vez de la topología aleatoria.
        /// </summary>
        public static DungeonManager CreateAndRegisterFromPlan(FloorTopologyPlanner.Plan plan)
        {
            var manager = new DungeonManager();
            ServiceLocator.AddService<IDungeonService>(manager, ServiceScope.Run);
            manager.GenerateFromPlan(plan);
            return manager;
        }

        /// <summary>
        /// Setea la <see cref="RoomState"/> de una sala desde afuera (gates externos —
        /// tutorial, boss key, evento). No toca <see cref="RoomInstance.ObjectStates"/>,
        /// así el toggle Locked↔Uncleared preserva HP de enemigos y flags de puertas.
        /// </summary>
        public bool SetRoomState(Guid instanceId, RoomState state)
        {
            if (!_instances.TryGetValue(instanceId, out var instance)) return false;
            instance.State = state;
            return true;
        }

        /// <summary>
        /// Refresca los visuales de puertas de una sala instanciada — necesario cuando
        /// un gate externo cambió el estado de una sala VECINA (la puerta que refleja
        /// ese lock vive en el prefab de esta sala).
        /// </summary>
        public void ResyncDoorVisuals(Guid instanceId)
        {
            if (_instances.TryGetValue(instanceId, out var instance))
                SyncDoorVisualStates(instance);
        }

        // -----------------------------------------------------------------
        // ISaveable (#0028) — estado espacial del piso
        // -----------------------------------------------------------------

        public string SaveKey => SaveKeyConst;

        /// <summary>
        /// Snapshot del estado espacial: sala actual + dirección de entrada, tile/GUID del
        /// player, y por cada sala <see cref="RoomState"/> + <c>Visited</c> +
        /// <see cref="RoomInstance.ObjectStates"/>. Indexado por <see cref="RoomInstance.GridCell"/>
        /// (estable), no por <see cref="RoomInstance.InstanceId"/> (se regenera).
        /// </summary>
        public object CaptureState()
        {
            var snap = new DungeonSnapshot { LastEntryDirection = _lastEntryDirection };

            var current = CurrentRoomInstance;
            if (current != null)
            {
                snap.CurrentCell = current.GridCell;
                // Vuelca HP/tile/GUID vivos del cuarto actual a sus EnemySpawnState — el HP
                // mid-combate solo vive en el stat runtime hasta acá (#0028 Fase 2).
                RoomEnemyStateSync.SnapshotLiveEnemies(current);
            }

            if (ServiceLocator.TryGetService<IPlayerService>(out var player)
                && player != null && player.PlayerGuid != Guid.Empty)
            {
                snap.PlayerGuid = player.PlayerGuid.ToString();
                _lastPlayerGuid = snap.PlayerGuid;

                if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null
                    && grid.TryGetPosition(player.PlayerGuid, out var pcoord))
                {
                    snap.PlayerCoord = pcoord;
                    _lastPlayerCoord = pcoord;
                }
                else if (_hasLastPlayer)
                {
                    snap.PlayerCoord = _lastPlayerCoord;
                }
                _hasLastPlayer = true;
            }
            else if (_hasLastPlayer)
            {
                // Player ya limpiado (teardown de EndRun): preservar los últimos valores
                // válidos en vez de defaultear a centro/empty (#0028).
                snap.PlayerGuid = _lastPlayerGuid;
                snap.PlayerCoord = _lastPlayerCoord;
            }

            foreach (var inst in _instances.Values)
            {
                var rs = new RoomSnapshot
                {
                    Cell = inst.GridCell,
                    State = inst.State,
                    Visited = inst.Visited,
                };
                foreach (var kv in inst.ObjectStates.Enumerate())
                    rs.ObjectStates[kv.Key] = kv.Value;
                snap.Rooms.Add(rs);
            }

            return snap;
        }

        /// <summary>
        /// Sólo <b>stagea</b> el snapshot — la aplicación real ocurre en
        /// <see cref="ResumeFromSave"/> una vez generada la topología.
        /// </summary>
        public void RestoreState(object state)
        {
            if (state is DungeonSnapshot snap) _pendingRestore = snap;
        }

        /// <summary>
        /// Aplica el snapshot stageado sobre la topología ya generada: pisa
        /// <c>State</c>/<c>Visited</c>/<c>ObjectStates</c> por <see cref="RoomInstance.GridCell"/>,
        /// restaura la sala actual + dirección de entrada, re-dispara
        /// <see cref="EventName.OnRoomEntered"/> (carga grilla + ubica al player en el spawn/puerta)
        /// y finalmente pisa la tile del player con la guardada. One-shot: consume el snapshot.
        /// No-op si no hay restore pendiente (run nueva). (Feature#0028)
        /// </summary>
        public void ResumeFromSave()
        {
            if (_pendingRestore == null) return;
            var snap = _pendingRestore;
            _pendingRestore = null;

            // 1. Pisa el estado por celda (match por GridCell — InstanceId se regenera).
            foreach (var rs in snap.Rooms)
            {
                if (rs == null) continue;
                if (!_cellIndex.TryGetValue(rs.Cell, out var id)) continue;
                if (!_instances.TryGetValue(id, out var inst)) continue;

                inst.State = rs.State;
                inst.Visited = rs.Visited;
                inst.ObjectStates.Clear();
                if (rs.ObjectStates != null)
                {
                    foreach (var kv in rs.ObjectStates)
                        if (kv.Value != null) inst.ObjectStates.Set(kv.Key, kv.Value);
                }
            }

            // 2. Sala actual + dirección de entrada.
            if (_cellIndex.TryGetValue(snap.CurrentCell, out var currentId))
                _currentId = currentId;
            _lastEntryDirection = snap.LastEntryDirection;

            // 3. Visibilidad + visuales de puerta de la sala actual (el estado que
            //    ConfigureDoorSlots sincronizó con el default quedó stale tras el pisón).
            RefreshRoomVisibility();
            var currentInst = CurrentRoomInstance;
            if (currentInst != null) SyncDoorVisualStates(currentInst);

            // 4. Re-entrar la sala guardada: RoomGridLoader (80) carga la grilla y
            //    PlayerRoomTransitioner (82) ubica al player en el spawn/puerta.
            EventManager.Trigger(EventName.OnRoomEntered, _currentId,
                CurrentRoom != null ? CurrentRoom.RoomId : string.Empty);

            // 5. Override de la tile exacta del player (post grid-load). Usa el GUID VIVO
            //    del player — la preservación del GUID guardado la maneja el restore de
            //    combate (#0028 Fase 3) para que la cola de turnos matchee.
            if (ServiceLocator.TryGetService<IPlayerService>(out var player)
                && player != null && player.PlayerGuid != Guid.Empty
                && ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                grid.Register(player.PlayerGuid, snap.PlayerCoord);
                if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals)
                    && visuals != null && visuals.TryGetPawn(player.PlayerGuid, out var pawn))
                {
                    pawn.SnapToGrid(grid, snap.PlayerCoord);
                }
            }
        }

        // -----------------------------------------------------------------
        // Internals
        // -----------------------------------------------------------------

        private bool TransitionTo(Guid neighborId)
        {
            if (!_instances.TryGetValue(neighborId, out var neighbor)) return false;

            // BUG-021 (hardening): choke point de TODO cruce de sala (click en casilla
            // de puerta, Pass Door, Force Door, dev commands). Un path del pawn en vuelo
            // se remapea al GridOrigin de la sala nueva y el player "sigue de largo" —
            // se cancela acá, antes del swap, sin depender de que PlayerRoomTransitioner
            // llegue a snapear (su early-return por prefab/layout faltante dejaba vivo
            // el path viejo).
            StopPlayerPawnMovement();

            DeactivateCurrentRoomCombat();

            _currentId = neighborId;
            MarkVisited(neighborId);

            RefreshRoomVisibility();

            EventManager.Trigger(EventName.OnRoomEntered, _currentId,
                CurrentRoom != null ? CurrentRoom.RoomId : string.Empty);
            return true;
        }

        private static void StopPlayerPawnMovement()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var player)
                || player == null || player.PlayerGuid == Guid.Empty) return;
            if (ServiceLocator.TryGetService<IEntityVisualService>(out var visuals)
                && visuals != null
                && visuals.TryGetPawn(player.PlayerGuid, out var pawn) && pawn != null)
            {
                pawn.StopMovement();
            }
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
        /// dirección, activa el DoorRoot y cablea el <see cref="DoorController"/>
        /// con <see cref="RoomInstance.InstanceId"/> + dirección; si no, apaga el
        /// DoorRoot entero (sin camino = sin puerta visible; el estado
        /// <see cref="DoorVisualState.Tapiada"/> igual queda seteado para los
        /// checks de interacción). El visual (Open = abierta, Locked = reja) lo
        /// resuelve <see cref="SyncDoorVisualStates"/>.
        /// </summary>
        private void ConfigureDoorSlots(RoomInstance instance)
        {
            if (instance?.SpawnedPrefab == null) return;

            var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null) return;

            // Boss room: designar dinámicamente la puerta OPUESTA a la única entrada como
            // salida de piso (#158). Se marca IsExit acá en runtime; el resto lo maneja el
            // flujo de puertas exit (skip de wall-plug abajo + apertura al clearear en
            // SyncDoorVisualStates). No requiere un IsExit autoreado en el prefab — cualquiera
            // de las 4 puertas cardinales puede terminar siendo la salida según la entrada.
            MarkBossExitDoor(instance, layout);

            // Track qué direcciones están autoreadas para detectar slots faltantes
            // contra Connections — un connection sin DoorSlotRef significa puerta
            // invisible/ no walkable y la sala vecina queda inalcanzable.
            var authored = new HashSet<DoorDirection>();

            foreach (var slot in layout.DoorSlots)
            {
                if (slot == null) continue;

                // Puerta de salida de piso en un slot (#158): la maneja
                // SyncDoorVisualStates — no aplicar wall-plug/connection (no tiene vecino).
                if (slot.DoorRoot != null)
                {
                    var slotCtrl = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                    if (slotCtrl != null && slotCtrl.IsExit)
                    {
                        authored.Add(slot.Direction);
                        continue;
                    }
                }

                bool connected = instance.Connections.ContainsKey(slot.Direction);

                // BUG-014: un DoorSlotRef sin DoorRoot no puede activar/desactivar su
                // puerta física. Si igual lo marcáramos como autoreado, el loop de
                // huérfanos de abajo saltearía esa dirección y un DoorController suelto
                // del prefab quedaría activo apuntando a la nada (puerta de tienda a
                // zona sin sala). Lo dejamos sin autorear para que ese loop lo tapie
                // o lo cablee según haya vecino.
                if (slot.DoorRoot == null)
                {
                    if (slot.WallPlug != null) slot.WallPlug.SetActive(!connected);
                    continue;
                }

                authored.Add(slot.Direction);

                // CNF-012 v2 rev: sin vecino el DoorRoot queda ACTIVO y muestra el zócalo
                // (wall-fill) que completa la pared — lo prende DoorController.SetState(Tapiada)
                // desde SyncDoorVisualStates. La reja es el visual de puerta bloqueada/forzable
                // y también la administra SetState — acá ya no se togglea el WallPlug del slot
                // (en los prefabs actuales es un GO hijo del DoorRoot).
                slot.DoorRoot.SetActive(true);

                if (!connected) continue;

                // Puerta hacia la boss room: swap del DoorRoot autorado por la variante
                // BossDoorPrefab ANTES de resolver el controller, así el wiring de abajo
                // y SyncDoorVisualStates operan sobre la puerta nueva sin cambios.
                if (LeadsToBossRoom(instance, slot.Direction))
                    TrySwapBossDoor(layout, slot);

                var controller = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                if (controller == null) continue;

                controller.OwnerRoomInstanceId = instance.InstanceId;
                controller.Direction = slot.Direction;
                controller.SpawnPointId = slot.Direction.DoorStateKey();
            }

            foreach (var connDir in instance.Connections.Keys)
            {
                if (!authored.Contains(connDir))
                {
                    var roomId = instance.Template != null ? instance.Template.RoomId : "<null>";
                    Debug.LogWarning(
                        $"[DungeonManager] Room '{roomId}' (cell {instance.GridCell}) tiene Connection " +
                        $"al {connDir} pero el prefab no tiene DoorSlotRef autoreado para esa dirección. " +
                        $"La sala vecina queda inalcanzable. Agregar DoorSlotRef[{connDir}] al prefab.");
                }
            }

            var allControllers = instance.SpawnedPrefab
                .GetComponentsInChildren<DoorController>(includeInactive: true);
            foreach (var controller in allControllers)
            {
                if (controller.IsExit) continue; // Exit doors: las maneja SyncDoorVisualStates (#158).
                if (authored.Contains(controller.Direction)) continue;

                var roomId = instance.Template != null ? instance.Template.RoomId : "<null>";
                bool connected = instance.Connections.ContainsKey(controller.Direction);

                if (connected)
                {
                    controller.OwnerRoomInstanceId = instance.InstanceId;
                    controller.SpawnPointId = controller.Direction.DoorStateKey();
                }
                else
                {
                    // Sin vecino: el GO queda activo para que el zócalo (wall-fill) de
                    // SetState(Tapiada) complete la pared, igual que las puertas con slot.
                    controller.SetState(DoorVisualState.Tapiada);
                }

                Debug.LogWarning(
                    $"[DungeonManager] Room '{roomId}' (cell {instance.GridCell}) tiene DoorController " +
                    $"dir={controller.Direction} sin DoorSlotRef. Usar Auto-Populate en RoomLayout.");
            }

            SyncDoorVisualStates(instance);
        }

        /// <summary>
        /// ¿El vecino conectado por <paramref name="dir"/> es la boss room? Excluye a la
        /// boss room misma como origen — sus puertas internas no cambian de modelo.
        /// </summary>
        private bool LeadsToBossRoom(RoomInstance instance, DoorDirection dir)
        {
            if (instance.Template == null || instance.Template.Type == RoomType.Boss) return false;
            if (!instance.Connections.TryGetValue(dir, out var neighborId)) return false;
            return _instances.TryGetValue(neighborId, out var neighbor)
                   && neighbor.Template != null
                   && neighbor.Template.Type == RoomType.Boss;
        }

        /// <summary>
        /// Reemplaza el DoorRoot autorado del slot por una instancia de
        /// <see cref="RoomLayout.BossDoorPrefab"/> (misma pose y parent) y re-apunta las
        /// referencias del slot (DoorRoot/Anchor/WallPlug) para que el resto del pipeline
        /// (wiring, SyncDoorVisualStates, DoorTileQuery, spawn frente a puerta) opere sobre
        /// la puerta nueva. No-op sin prefab; defensivo si el prefab no trae DoorController.
        /// </summary>
        private static void TrySwapBossDoor(RoomLayout layout, DoorSlotRef slot)
        {
            if (layout.BossDoorPrefab == null || slot.DoorRoot == null) return;

            var original = slot.DoorRoot;
            var swapped = UnityEngine.Object.Instantiate(
                layout.BossDoorPrefab, original.transform.parent);
            swapped.transform.localPosition = original.transform.localPosition;
            swapped.transform.localRotation = original.transform.localRotation;
            swapped.transform.localScale    = original.transform.localScale;
            swapped.SetActive(true);

            var swappedCtrl = swapped.GetComponentInChildren<DoorController>(includeInactive: true);
            if (swappedCtrl == null)
            {
                Debug.LogWarning(
                    $"[DungeonManager] BossDoorPrefab '{layout.BossDoorPrefab.name}' no tiene " +
                    "DoorController — se conserva la puerta original.");
                DestroyDoorRoot(swapped);
                return;
            }

            slot.DoorRoot = swapped;
            // Anchor = transform del controller en los prefabs reales (AutoPopulateDoorSlots);
            // solo se re-apunta si vivía dentro del root reemplazado.
            if (slot.Anchor != null && slot.Anchor.IsChildOf(original.transform))
                slot.Anchor = swappedCtrl.transform;
            if (slot.WallPlug != null && slot.WallPlug.transform.IsChildOf(original.transform))
                slot.WallPlug = swappedCtrl.WallPlugRef;

            // Unparent primero: lo saca ya mismo del GetComponentsInChildren del loop de
            // huérfanos aunque el Destroy de play mode sea diferido.
            original.transform.SetParent(null);
            DestroyDoorRoot(original);
        }

        private static void DestroyDoorRoot(GameObject go)
        {
            // Destroy() está prohibido fuera de play mode — los tests EditMode
            // pasan por acá (mismo patrón que ClearState).
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Recorre todas las instancias y verifica que cada conexión tenga
        /// reciprocidad de DoorSlotRef en ambas rooms — si una room tiene
        /// slot al East pero su vecina no tiene slot al West, la puerta queda
        /// asimétrica y el jugador no puede volver. Solo loggea, no fixea.
        /// </summary>
        private void AuditDoorSlotReciprocity()
        {
            foreach (var instance in _instances.Values)
            {
                if (instance.SpawnedPrefab == null) continue;
                var layout = instance.SpawnedPrefab.GetComponent<RoomLayout>();
                if (layout == null) continue;

                foreach (var slot in layout.DoorSlots)
                {
                    if (slot == null) continue;
                    if (!instance.Connections.TryGetValue(slot.Direction, out var neighborId)) continue;
                    if (!_instances.TryGetValue(neighborId, out var neighbor)) continue;
                    if (neighbor.SpawnedPrefab == null) continue;

                    var neighborLayout = neighbor.SpawnedPrefab.GetComponent<RoomLayout>();
                    if (neighborLayout == null) continue;

                    var opposite = slot.Direction.Opposite();
                    bool reciprocal = false;
                    foreach (var ns in neighborLayout.DoorSlots)
                    {
                        if (ns != null && ns.Direction == opposite) { reciprocal = true; break; }
                    }
                    if (!reciprocal)
                    {
                        var roomId = instance.Template != null ? instance.Template.RoomId : "<null>";
                        var neighborRoomId = neighbor.Template != null ? neighbor.Template.RoomId : "<null>";
                        Debug.LogWarning(
                            $"[DungeonManager] Asimetría de puertas: '{roomId}' (cell {instance.GridCell}) " +
                            $"tiene slot {slot.Direction} hacia '{neighborRoomId}' (cell {neighbor.GridCell}), " +
                            $"pero el vecino no tiene slot {opposite}. Agregar DoorSlotRef[{opposite}] al prefab vecino.");
                    }
                }
            }
        }

        /// <summary>
        /// Fog of war: solo el <see cref="CurrentRoomInstance"/> queda activo
        /// en escena. Los shells procedurales del floor view (§17.E.9) no se
        /// tocan — siguen visibles como minimap zoom-out.
        /// </summary>
        /// <summary>Marca la sala como visitada (fog of war del floor view). No-op si el id no existe.</summary>
        private void MarkVisited(Guid id)
        {
            if (_instances.TryGetValue(id, out var instance)) instance.Visited = true;
        }

        private void RefreshRoomVisibility()
        {
            foreach (var instance in _instances.Values)
            {
                if (instance.SpawnedPrefab == null) continue;
                bool isCurrent = instance.InstanceId == _currentId;
                if (instance.SpawnedPrefab.activeSelf != isCurrent)
                {
                    instance.SpawnedPrefab.SetActive(isCurrent);
                }
            }
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

                // Vecino Locked (gate externo) → LockedSkillCheck, con precedencia
                // sobre Open/LockedCombat — espeja el check de CanEnterRoomByDoor.
                if (instance.Connections.TryGetValue(slot.Direction, out var neighborId)
                    && _instances.TryGetValue(neighborId, out var neighbor)
                    && neighbor.State == RoomState.Locked)
                {
                    controller.SetState(DoorVisualState.LockedSkillCheck);
                    continue;
                }

                instance.ObjectStates.TryGet<DoorState>(slot.Direction.DoorStateKey(), out var doorState);
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

            // Puertas de salida de piso (#158): siempre visibles; Open al clearear la sala
            // (boss derrotado). El mesh "open" distinguible es el efecto visual de habilitación.
            foreach (var controller in instance.SpawnedPrefab.GetComponentsInChildren<DoorController>(includeInactive: true))
            {
                if (!controller.IsExit) continue;

                controller.OwnerRoomInstanceId = instance.InstanceId;
                if (string.IsNullOrEmpty(controller.SpawnPointId))
                    controller.SpawnPointId = "exit_" + controller.Direction.DoorStateKey();

                if (!controller.gameObject.activeSelf) controller.gameObject.SetActive(true);
                controller.SetState(instance.State == RoomState.Cleared
                    ? DoorVisualState.Open
                    : DoorVisualState.LockedCombat);
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
                var key = dir.DoorStateKey();
                if (!instance.ObjectStates.TryGet<DoorState>(key, out var doorState)) continue;
                doorState.Unlocked = true;
            }

            if (_currentId == instance.InstanceId)
            {
                SyncDoorVisualStates(instance);
            }

            EventManager.Trigger(EventName.OnRoomCleared, roomInstanceId);

            // Boss clareada. La victoria del floor ya NO se dispara acá de una: el canal
            // Personaje (Upgrades.Character) reacciona al OnRoomCleared de arriba, spawnea
            // los pedestales de reward, y dispara OnFloorCleared recién cuando el player
            // elige una mejora (o de inmediato si no hay rewards para ofrecer). Solo lo
            // disparamos nosotros como fallback si ese canal no está activo (builds/tests
            // sin el bootstrap), para no dejar el floor sin cerrar.
            if (instance.Template != null && instance.Template.Type == RoomType.Boss
                && (!ServiceLocator.TryGetService<ICharacterRewardService>(out var rewards) || rewards == null))
            {
                // Schema OnFloorCleared: [Guid runId, int floorIndex] (BUG-022: antes
                // mandaba roomInstanceId y 0 — achievements/analytics parseaban mal).
                ServiceLocator.TryGetService<Rollgeon.Run.IRunContextService>(out var runCtx);
                EventManager.Trigger(EventName.OnFloorCleared,
                    runCtx != null ? runCtx.RunId : Guid.Empty,
                    runCtx != null ? runCtx.FloorIndex : 0);
            }
        }

        /// <summary>
        /// Hook global de muertes — cuando un enemigo registrado por el resolver
        /// en <see cref="RoomInstance.SpawnedEnemies"/> cae, actualiza su
        /// <see cref="EnemySpawnState.IsDead"/>. La señal de victoria la dispara
        /// <c>CombatDeathWatcher</c> cuando <c>SpawnedEnemies.Count == 0</c>.
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

                return;
            }
        }

        private void ClearState()
        {
            foreach (var instance in _instances.Values)
            {
                if (instance.SpawnedPrefab != null)
                {
                    // Destroy() está prohibido fuera de play mode — los tests EditMode
                    // que spawn-ean prefabs reales necesitan el camino Immediate.
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(instance.SpawnedPrefab);
                    else
                        UnityEngine.Object.DestroyImmediate(instance.SpawnedPrefab);
                    instance.SpawnedPrefab = null;
                }
                instance.SpawnedEnemies.Clear();
            }

            _instances.Clear();
            _shells.Clear();
            _cellIndex.Clear();
            _currentId = Guid.Empty;
            _lastEntryDirection = null;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private Guid FindStartInstanceId()
        {
            // El start siempre es Vector2Int.zero (ver FloorTopologyPlanner).
            return _cellIndex[Vector2Int.zero];
        }

        /// <summary>
        /// Garantiza que cada boss room tenga exactamente UNA conexión (entrada) — un
        /// dead-end (#158). Si la topología la dejó con varias, conserva la primera (en
        /// orden N/S/E/W) cuya poda del resto mantenga TODO el piso alcanzable desde el
        /// start, y remueve las demás (con su recíproca en el vecino). La puerta opuesta
        /// a la entrada se vuelve la salida de piso dinámica.
        /// </summary>
        private void EnforceBossSingleEntrance()
        {
            foreach (var instance in _instances.Values)
            {
                if (instance.Template == null || instance.Template.Type != RoomType.Boss) continue;
                if (instance.Connections.Count <= 1) continue;

                var dirs = new List<DoorDirection>(instance.Connections.Keys);
                DoorDirection keep = dirs[0];
                bool found = false;
                foreach (var dir in dirs)
                {
                    if (StaysFullyConnectedKeepingOnly(instance, dir)) { keep = dir; found = true; break; }
                }
                if (!found)
                {
                    Debug.LogWarning(
                        $"[DungeonManager] Boss room (cell {instance.GridCell}): no pude reducir a 1 " +
                        $"entrada sin desconectar el piso; conservo {keep}.");
                }

                foreach (var dir in dirs)
                {
                    if (dir == keep) continue;
                    if (!instance.Connections.TryGetValue(dir, out var neighborId)) continue;
                    instance.Connections.Remove(dir);
                    if (_instances.TryGetValue(neighborId, out var neighbor))
                        neighbor.Connections.Remove(dir.Opposite());
                }
            }
        }

        /// <summary>
        /// ¿Sigue todo el piso alcanzable desde el start si el <paramref name="boss"/>
        /// conservara solo la conexión <paramref name="keepDir"/>? BFS sobre el grafo de
        /// conexiones tratando al boss como si tuviera únicamente esa arista (ida y vuelta).
        /// </summary>
        private bool StaysFullyConnectedKeepingOnly(RoomInstance boss, DoorDirection keepDir)
        {
            if (!_cellIndex.TryGetValue(Vector2Int.zero, out var startId)) return true;

            var visited = new HashSet<Guid> { startId };
            var queue = new Queue<Guid>();
            queue.Enqueue(startId);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                var inst = _instances[id];
                foreach (var (dir, neighborId) in inst.Connections)
                {
                    // Del boss solo sale la arista keepDir.
                    if (id == boss.InstanceId && dir != keepDir) continue;
                    // Hacia el boss solo entra la recíproca de keepDir.
                    if (neighborId == boss.InstanceId && dir != keepDir.Opposite()) continue;
                    if (visited.Add(neighborId)) queue.Enqueue(neighborId);
                }
            }

            return visited.Count == _instances.Count;
        }

        /// <summary>
        /// PUL-011: poda las conexiones intraversables — las que caen en una dirección sin
        /// puerta autorada en el prefab (o cuyo vecino no tiene la recíproca). Deja el grafo
        /// lógico coherente con las puertas físicas (el minimapa deja de mostrar un paso que
        /// no existe) y loguea un <see cref="Debug.LogError"/> por cada poda para exponer el
        /// prefab mal autoreado. Tras podar, avisa si alguna sala quedó inalcanzable.
        /// </summary>
        private void PruneDoorlessConnections()
        {
            var edges = DoorTopologyGuard.ComputeDoorlessEdges(_instances, PrefabHasDoorForDirection);
            if (edges.Count == 0) return;

            foreach (var e in edges)
            {
                if (_instances.TryGetValue(e.From, out var a)) a.Connections.Remove(e.Dir);
                if (_instances.TryGetValue(e.To, out var b)) b.Connections.Remove(e.Dir.Opposite());

                Debug.LogError(
                    $"[DungeonManager] Conexión sin puerta podada: '{RoomIdOf(e.From)}' {e.Dir} ↔ " +
                    $"'{RoomIdOf(e.To)}'. Un prefab no tiene DoorSlot autoreado ({e.Dir} en el origen " +
                    $"o {e.Dir.Opposite()} en el vecino); la conexión era intraversable y se quitó del " +
                    "grafo. Autorar las 4 puertas del prefab (Auto-Populate en el RoomLayout).");
            }

            // ¿Se aisló alguna sala al podar? Softlock si estaba en el camino crítico — error explícito.
            if (!_cellIndex.TryGetValue(Vector2Int.zero, out var startId)) return;
            var reachable = DoorTopologyGuard.ReachableFrom(startId, _instances);
            foreach (var inst in _instances.Values)
            {
                if (reachable.Contains(inst.InstanceId)) continue;
                Debug.LogError(
                    $"[DungeonManager] Sala '{RoomIdOf(inst.InstanceId)}' (cell {inst.GridCell}) quedó " +
                    "INALCANZABLE desde el start tras podar conexiones sin puerta. Revisar las puertas " +
                    "autoradas de esa sala y sus vecinas.");
            }
        }

        /// <summary>
        /// ¿El prefab de <paramref name="instance"/> tiene una puerta autorada para
        /// <paramref name="dir"/>? Mira el <c>RoomLayout.DoorSlots</c> del prefab template
        /// (un slot de esa dirección con <c>DoorRoot</c>) y, como fallback, un
        /// <see cref="DoorController"/> suelto. Sin prefab/layout devuelve <c>true</c> (no
        /// podemos determinar ⇒ no podar). Mismo criterio de "autoreado" que
        /// <see cref="ConfigureDoorSlots"/>.
        /// </summary>
        private bool PrefabHasDoorForDirection(RoomInstance instance, DoorDirection dir)
        {
            var prefab = instance?.Template != null ? instance.Template.RoomPrefab : null;
            if (prefab == null) return true;

            var layout = prefab.GetComponent<RoomLayout>();
            if (layout == null || layout.DoorSlots == null) return true;

            foreach (var slot in layout.DoorSlots)
                if (slot != null && slot.Direction == dir && slot.DoorRoot != null)
                    return true;

            foreach (var ctrl in prefab.GetComponentsInChildren<DoorController>(includeInactive: true))
                if (ctrl != null && ctrl.Direction == dir)
                    return true;

            return false;
        }

        private string RoomIdOf(Guid id)
            => _instances.TryGetValue(id, out var inst) && inst.Template != null ? inst.Template.RoomId : "<null>";

        /// <summary>
        /// Marca como salida de piso (<see cref="DoorController.IsExit"/>) la puerta de la
        /// boss room OPUESTA a su única entrada (#158). No-op si no es boss o no tiene
        /// exactamente 1 conexión. La entrada queda como puerta normal; las 2 perpendiculares,
        /// tapiadas. Así cualquiera de las 4 cardinales puede ser la salida según la entrada,
        /// sin rotar la sala ni una puerta fija.
        /// </summary>
        private static void MarkBossExitDoor(RoomInstance instance, RoomLayout layout)
        {
            if (instance?.Template == null || instance.Template.Type != RoomType.Boss) return;
            if (instance.SpawnedPrefab == null) return;

            // En la boss room la salida es 100% dinámica: reseteamos cualquier IsExit
            // autoreado para nunca terminar con 2 salidas.
            foreach (var c in instance.SpawnedPrefab.GetComponentsInChildren<DoorController>(includeInactive: true))
                c.IsExit = false;

            if (instance.Connections.Count != 1 || layout.DoorSlots == null) return;

            DoorDirection entranceDir = default;
            foreach (var d in instance.Connections.Keys) { entranceDir = d; break; }
            var exitDir = entranceDir.Opposite();

            foreach (var slot in layout.DoorSlots)
            {
                if (slot == null || slot.Direction != exitDir || slot.DoorRoot == null) continue;
                var ctrl = slot.DoorRoot.GetComponentInChildren<DoorController>(includeInactive: true);
                if (ctrl != null) ctrl.IsExit = true;
            }
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

        private Vector3 ComputeStepSize(IReadOnlyDictionary<Vector2Int, RoomSO> assignments)
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

    }
}
