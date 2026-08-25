using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Threat;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities.Portraits;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.Rooms
{
    /// <summary>
    /// Mantiene los objetos de sala de un <see cref="RoomObjectDefinitionSO"/>: los coloca la primera
    /// vez según <see cref="Pattern"/>, detecta los rotos, deja el hazard de muerte en su casilla y
    /// los repone en la MISMA ranura pasado el delay de la definición.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>NO envolver en <c>Once</c>.</b> El nodo se auto-gatea pero necesita tickear cada turno para
    /// correr los relojes de reposición; envuelto en <c>Once</c> queda latcheado tras el primer
    /// spawn y ningún objeto vuelve nunca. Devuelve <see cref="AIResult.Succeeded"/> en los ticks de
    /// espera para no abortar el Sequence del jefe.
    /// </para>
    /// <para>
    /// Orden del tick: recoger rotos → correr relojes → reponer. Las casillas se resuelven una vez y
    /// se recuerdan, así el objeto vuelve donde estaba — salvo con
    /// <see cref="ResolveSlotsEachSpawn"/>, que re-sortea contra <see cref="Pattern"/> en cada
    /// reposición.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SpawnRoomObjects : AIActionNode, IAIOpeningNode
    {
        /// <summary>Cómo se eligen las casillas de las ranuras.</summary>
        public enum Placement
        {
            /// <summary>Fila de <see cref="Count"/> casillas consecutivas a un lado del jefe, creciendo
            /// a lo ancho de la pared. La forma de una máquina atornillada al fondo de la sala.</summary>
            RowNextToSelf = 0,

            /// <summary>Anillos alrededor del jefe, de adentro hacia afuera, hasta juntar
            /// <see cref="Count"/>. La forma de lo que el jefe acaba de tirar sobre la mesa.</summary>
            RingAroundSelf = 1,

            /// <summary>Offsets relativos al jefe, autorados en <see cref="Coords"/>.</summary>
            OffsetsFromSelf = 2,

            /// <summary>Coordenadas absolutas de sala, autoradas en <see cref="Coords"/>.</summary>
            ExplicitCoords = 3,

            /// <summary>Casillas libres al azar de toda la sala.</summary>
            ScatteredFree = 4,

            /// <summary>La casilla interior frente a cada puerta de la sala, y lo que sobra de
            /// <see cref="Count"/> al anillo de <see cref="RingAroundSelf"/>. La forma de la mesa
            /// repartida entre las cuatro salidas: cruzarla bajo persecución paga un precio,
            /// pagarle el filo al jefe paga otro.</summary>
            DoorFronts = 5,
        }

        /// <summary>Lado del jefe donde se alinea la fila de <see cref="Placement.RowNextToSelf"/>.</summary>
        public enum RowSide
        {
            /// <summary>Elige el lado con más casillas válidas — el que no da a la pared.</summary>
            Auto = 0,
            Down = 1,
            Up = 2,
            Left = 3,
            Right = 4,
        }

        [OdinSerialize]
        [Tooltip("Definición del objeto a instanciar. Ver RoomObjectDefinitionSO.")]
        public RoomObjectDefinitionSO Definition;

        [Tooltip("Cantidad de ranuras a abrir. Ignorado por ExplicitCoords y OffsetsFromSelf: ahí la " +
                 "cantidad la manda la lista Coords.")]
        [MinValue(1)]
        public int Count = 3;

        [Tooltip("Forma en la que se reparten las ranuras.")]
        public Placement Pattern = Placement.RowNextToSelf;

        [ShowIf(nameof(Pattern), Placement.RowNextToSelf)]
        [Tooltip("Lado del jefe donde se alinea la fila. Auto = el lado con más casillas válidas.")]
        public RowSide Side = RowSide.Auto;

        [ShowIf(nameof(Pattern), Placement.ScatteredFree)]
        [MinValue(0)]
        [Tooltip("Separación mínima (Chebyshev) entre ranuras de ScatteredFree, y entre cada ranura y " +
                 "el jefe. 0 = sin separación forzada (default, el comportamiento de siempre).")]
        public int MinSpacing = 0;

        [Tooltip("Al reponer una ranura, re-sortea su casilla contra Pattern en vez de volver a la " +
                 "misma donde estaba. Default false: La Generala necesita que sus dados vuelvan al " +
                 "mismo lugar.")]
        public bool ResolveSlotsEachSpawn = false;

        [Tooltip("Casillas autoradas: absolutas con Pattern = ExplicitCoords, relativas al jefe con " +
                 "Pattern = OffsetsFromSelf. Ignorado por el resto de los patrones.")]
        public List<GridCoord> Coords = new List<GridCoord>();

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Gesto del jefe al colocar o reponer objetos, sólo en los ticks que ponen algo de " +
                 "verdad. Vacío = sin animación.")]
        public string SpawnFeedbackId;

        /// <summary>Anillos que <see cref="Placement.RingAroundSelf"/> llega a abrir antes de rendirse.
        /// Más lejos que esto el objeto ya no lee como "del jefe" y conviene otro patrón.</summary>
        private const int MaxRingRadius = 4;

        // --- Runtime state (per-combat). NonSerialized: vive solo en la copia runtime del árbol
        // (EnemyDataSO.CreateRuntimeAIRoot → SerializationUtility.CreateCopy), nunca en el asset.
        // Mismo patrón que AINode_SpawnReinforcements: una pelea nueva arranca con _slots en null
        // ⇒ las ranuras se re-resuelven contra la sala nueva.
        [NonSerialized] private List<Slot> _slots;

        /// <summary>
        /// Si el último <see cref="Tick"/> colocó algún objeto. El nodo devuelve
        /// <see cref="AIResult.Succeeded"/> también en los ticks de espera, así que sin esto el gesto
        /// de reponer correría todos los turnos con la mesa entera en pie.
        /// </summary>
        [NonSerialized] private bool _spawnedThisTick;

        private sealed class Slot
        {
            public GridCoord Coord;

            /// <summary><see cref="Guid.Empty"/> = ranura vacía (nunca llenada, o rota).</summary>
            public Guid ObjectGuid;

            public int TurnsUntilRespawn;

            /// <summary>
            /// El último guid que ocupó la ranura; sobrevive a la rotura. Es lo que se le publica al
            /// servicio de armadura: mandarle <see cref="Guid.Empty"/> al romperse le saca el guid
            /// antes de que pueda mirarle la vida, y la ranura le queda intacta para siempre.
            /// </summary>
            public Guid LastObjectGuid;

            /// <summary>La ranura no vuelve a llenarse: la definición no repone.</summary>
            public bool Retired;
        }

        public override string NodeName =>
            $"Spawn Room Objects ({Count}x {(Definition != null ? Definition.name : "?")}, {Pattern})";

        /// <summary>Ranuras abiertas. 0 antes del primer tick.</summary>
        public int SlotCount => _slots?.Count ?? 0;

        /// <summary>
        /// Estado de una ranura: su casilla y el guid del objeto que la ocupa
        /// (<see cref="Guid.Empty"/> si está vacía). <c>false</c> si el índice no existe.
        /// </summary>
        public bool TryGetSlot(int index, out GridCoord coord, out Guid objectGuid)
        {
            coord = default;
            objectGuid = Guid.Empty;
            if (_slots == null || index < 0 || index >= _slots.Count) return false;

            coord = _slots[index].Coord;
            objectGuid = _slots[index].ObjectGuid;
            return true;
        }

        /// <summary>
        /// Guid y casilla de cada ranura en pie. Lo consume por composición quien necesite saber qué
        /// sobrevivió sin quedarse con la lista mutable: las bombas del Croupier deciden con esto
        /// cuáles detonar.
        /// </summary>
        public IEnumerable<(Guid Guid, GridCoord Coord)> LiveObjects()
        {
            if (_slots == null) yield break;

            foreach (var slot in _slots)
            {
                if (slot.ObjectGuid == Guid.Empty) continue;
                yield return (slot.ObjectGuid, slot.Coord);
            }
        }

        public override AIResult Tick(AIContext context)
        {
            _spawnedThisTick = false;
            if (context == null || Definition == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null || context.Attributes == null) return AIResult.Failed;

            if (_slots == null)
            {
                var coords = ResolveSlotCoords(context, grid);
                if (coords == null || coords.Count == 0)
                {
                    Debug.LogWarning($"[AINode_SpawnRoomObjects] '{Definition.EffectiveDisplayName}': el patrón " +
                                     $"{Pattern} no encontró casillas válidas — no se coloca nada.");
                    return AIResult.Failed;
                }

                _slots = new List<Slot>(coords.Count);
                foreach (var c in coords) _slots.Add(new Slot { Coord = c });
            }

            CollectBroken(context, grid);
            RefillSlots(context, grid);
            PublishArmor(context);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Mismo trabajo que el tick: la mesa es la referencia espacial de la pelea, y apareciendo
        /// recién al cerrar el primer turno el jugador ya eligió por dónde entrar a ciegas.
        /// </summary>
        /// <remarks>
        /// Si el jefe todavía no está en el grid, <see cref="ResolveSlotCoords"/> no encuentra
        /// casillas y esto no coloca nada: la colocación vuelve a caer en el primer tick del jefe,
        /// que es el comportamiento de antes.
        /// </remarks>
        public void Opening(AIContext context) => Tick(context);

        /// <summary>
        /// Le pasa el estado de las ranuras a <see cref="RoomObjectArmorService"/> cuando la definición
        /// otorga armadura. Sólo publica guids: quién está roto lo decide el servicio al consultar, no
        /// acá.
        /// </summary>
        /// <remarks>
        /// Ese reparto es a propósito. Este nodo tickea en el turno del jefe, así que congelar la cuenta
        /// acá haría que romper un objeto en el turno del jugador no bajara la reducción hasta el turno
        /// siguiente — y el golpe de después seguiría reducido, que se lee como que el juego no registró
        /// el impacto.
        /// </remarks>
        private void PublishArmor(AIContext context)
        {
            if (!Definition.GrantsOwnerArmor) return;
            if (context.SelfGuid == Guid.Empty) return;

            var guids = new Guid[_slots.Count];
            for (int i = 0; i < _slots.Count; i++) guids[i] = _slots[i].LastObjectGuid;

            RoomObjectArmorService.ResolveOrCreate()
                .Publish(context.SelfGuid, guids, Definition.OwnerDamageReductionPerObject);
        }

        // ======================================================================
        // Ciclo de rotura / reposición
        // ======================================================================

        /// <summary>
        /// Vacía toda ranura cuyo objeto ya no tenga vida, deja el hazard de muerte en su casilla y
        /// arranca su reloj. Misma fuente de verdad que el alive-check de la AI de targeting: sin
        /// <see cref="Health"/> registrada o en 0 = muerto.
        /// </summary>
        private void CollectBroken(AIContext context, IGridManager grid)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.ObjectGuid == Guid.Empty) continue;

                var health = context.Attributes.GetAttribute<Health>(slot.ObjectGuid);
                if (health != null && health.Value > 0) continue;

                // Se libera la casilla acá aunque CombatDeathWatcher ya lo haga en la muerte que pasa
                // por él: un objeto puede irse por otra vía (un hazard, un cambio de fase) y una
                // ranura rota que sigue en el mapa de ocupancia es un muro invisible.
                grid.Unregister(slot.ObjectGuid);

                LeaveDeathHazard(slot.Coord);

                slot.ObjectGuid = Guid.Empty;
                if (Definition.Respawns) slot.TurnsUntilRespawn = Definition.RespawnDelayTurns;
                else slot.Retired = true;
            }
        }

        private void LeaveDeathHazard(GridCoord coord)
        {
            if (Definition.OnDeathHazard == null) return;
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null) return;

            // Overload de tiles, no la de definición: el fuego que deja el objeto vive en SU casilla,
            // no en la forma que el hazard usa cuando lo tira el jefe. Y cada rotura es una instancia
            // independiente, así que dos objetos rotos no se pisan la llama.
            hazards.Activate(Definition.OnDeathHazard, new[] { coord });
        }

        /// <summary>
        /// Corre el reloj de cada ranura vacía y repone las que llegaron a 0.
        /// </summary>
        private void RefillSlots(AIContext context, IGridManager grid)
        {
            // Resuelta una sola vez por tick, no por ranura: con varias ranuras vacías a la vez
            // (una ola entera repuesta junta) cada una tiene que leer contra el mismo sorteo, no
            // contra sorteos sucesivos que se van pisando entre sí.
            List<GridCoord> freshCoords = null;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Retired || slot.ObjectGuid != Guid.Empty) continue;

                if (slot.TurnsUntilRespawn > 0)
                {
                    slot.TurnsUntilRespawn--;
                    continue;
                }

                if (ResolveSlotsEachSpawn)
                {
                    freshCoords ??= ResolveSlotCoords(context, grid);
                    if (freshCoords != null && i < freshCoords.Count) slot.Coord = freshCoords[i];
                }

                // Ranura pisada (el jugador parado ahí) o sin piso: se reintenta el próximo turno. El
                // reloj ya está en 0, así que la espera no acumula deuda.
                if (!IsPlaceable(grid, slot.Coord)) continue;

                slot.ObjectGuid = Spawn(context, grid, slot.Coord);
                if (slot.ObjectGuid != Guid.Empty) slot.LastObjectGuid = slot.ObjectGuid;
                _spawnedThisTick = true;
            }
        }

        // ======================================================================
        // Presentación
        // ======================================================================

        /// <remarks>
        /// <para>
        /// Los objetos aparecían de la nada: cinco dados se materializaban mientras la jefa seguía en
        /// idle, y nada decía que los había puesto ella. Con el gesto, poner y reponer la mesa se lee
        /// como una acción suya. Mismo criterio —y mismo problema— que documenta
        /// <c>AINode_SpawnReinforcements</c>.
        /// </para>
        /// <para>
        /// Sólo en los ticks que colocan algo: el nodo también devuelve <c>Succeeded</c> mientras
        /// espera (mesa en pie, reloj de reposición corriendo), y animar esos turnos sería el jefe
        /// invocando al aire.
        /// </para>
        /// </remarks>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);
            if (result != AIResult.Succeeded || !_spawnedThisTick || string.IsNullOrEmpty(SpawnFeedbackId))
            {
                onResult?.Invoke(result);
                yield break;
            }

            var beat = PlaySpawn(context);
            while (beat.MoveNext()) yield return beat.Current;

            onResult?.Invoke(result);
        }

        /// <remarks>
        /// Request armado a mano en vez de un <c>EffPlaySequence</c>: el nodo no nace de un effect
        /// pass, así que no tiene <c>EffectContext</c> que pasarle — mismo caso que el resto de los
        /// nodos de jefe.
        /// </remarks>
        private IEnumerator PlaySpawn(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = SpawnFeedbackId,
                StartMode = StepStartMode.Immediate,
                EndMode = StepEndMode.OnDuration,
                BlockSequence = true,
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep> { step },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el turno no se
            // retiene. Mismo degradado que EffPlaySequence.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

#if UNITY_EDITOR
        // Dropdown obligatorio (§0): los ids de feedback nunca se tipean a mano.
        private static IEnumerable<string> GetFeedbackIdsForDropdown()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FeedbackDBSO"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var db = UnityEditor.AssetDatabase.LoadAssetAtPath<FeedbackDBSO>(path);
                if (db == null) continue;
                foreach (var id in db.GetAllFeedbackIds()) yield return id;
            }
        }
#endif

        // ======================================================================
        // Spawn
        // ======================================================================

        private Guid Spawn(AIContext context, IGridManager grid, GridCoord coord)
        {
            var id = Guid.NewGuid();
            int hp = Definition.EffectiveHp;

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            // El paso de escudo del DamagePipeline lee y escribe Shield; todo lo demás que entra a
            // combate lo trae desde CreateRuntimeStats, y un objeto sin el atributo sería el único
            // caso especial del pipeline.
            attrs.SetAttribute<Shield>(new Shield(0));

            context.Attributes.Register(id, attrs);

            // Opcional a propósito: el registry es el stub de iniciativa y sólo hace falta si el
            // objeto llega a pedir turno. Un objeto de sala no debe fallar su spawn por no tenerlo.
            if (ServiceLocator.TryGetService<InMemoryEntityRegistry>(out var registry) && registry != null)
                registry.Register(id, attrs);

            if (Definition.Blocks) grid.Register(id, coord);

            SpawnPawn(context, id, coord, hp);

            if (!Definition.HideFromTurnQueue) JoinTurnQueue(id);

            return id;
        }

        /// <summary>
        /// Pawn del objeto vía <c>SpawnProp</c>, no <c>SpawnEnemy</c>: entra como prop y no arrastra
        /// la presentación de enemigo. La barra de vida sale del prefab — es ahí donde vive el anillo
        /// en la base que pide el diseño, en vez de la barra flotante de bicho.
        /// </summary>
        private void SpawnPawn(AIContext context, Guid id, GridCoord coord, int hp)
        {
            var visuals = context.VisualService;
            if (visuals == null) return;

            if (Definition.VisualPrefab == null)
            {
                Debug.LogWarning($"[AINode_SpawnRoomObjects] '{Definition.EffectiveDisplayName}' sin VisualPrefab — " +
                                 "el objeto bloquea y recibe daño pero no se ve.");
                return;
            }

            var pawn = visuals.SpawnProp(id, Definition.VisualPrefab, coord);
            if (pawn != null && pawn.HealthBar != null) pawn.HealthBar.Initialize(id, hp, hp);
        }

        /// <summary>
        /// Suma el objeto a la ronda en curso cuando la definición NO lo esconde. Lo caro de estar en
        /// la cola es no comportarse como un enemigo: hace falta un árbol no-op registrado (sin él
        /// <c>TreeDrivenEnemyAI</c> cae al <c>BasicEnemyAI</c> y el objeto ataca al jugador) y un
        /// retrato (el slot sale en blanco si nadie lo registró).
        /// </summary>
        private void JoinTurnQueue(Guid id)
        {
            if (ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry) && aiRegistry != null)
                aiRegistry.Register(id, new AINode_Wait(), Definition.EffectiveHp);

            if (ServiceLocator.TryGetService<IEntityPortraitResolver>(out var portraits) && portraits != null)
                portraits.Register(id, Definition.Portrait);

            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null) return;

            turnOrder.Append(id);

            // Mismo diferido que un refuerzo: entra a la ronda EN CURSO, así que sin este aviso
            // consumiría su turno de aparición. En un objeto es cosmético (su árbol es un Wait) pero
            // lo mantiene alineado con el resto del spawn mid-combate.
            EventManager.Trigger(EventName.OnReinforcementSpawned, id);
        }

        // ======================================================================
        // Patrones
        // ======================================================================

        private List<GridCoord> ResolveSlotCoords(AIContext context, IGridManager grid)
        {
            switch (Pattern)
            {
                case Placement.ExplicitCoords: return PlaceableSubset(Coords, grid);
                case Placement.OffsetsFromSelf: return OffsetSlots(context, grid);
                case Placement.RowNextToSelf: return RowSlots(context, grid);
                case Placement.RingAroundSelf: return RingSlots(context, grid);
                case Placement.ScatteredFree: return ScatteredSlots(context, grid);
                case Placement.DoorFronts: return DoorFrontSlots(context, grid);
                default: return null;
            }
        }

        /// <summary>Casillas de <paramref name="candidates"/> donde el objeto entra, sin repetidas y
        /// en el orden autorado.</summary>
        private List<GridCoord> PlaceableSubset(IEnumerable<GridCoord> candidates, IGridManager grid)
        {
            var result = new List<GridCoord>();
            if (candidates == null) return result;

            foreach (var c in candidates)
            {
                if (!IsPlaceable(grid, c) || result.Contains(c)) continue;
                result.Add(c);
            }
            return result;
        }

        private List<GridCoord> OffsetSlots(AIContext context, IGridManager grid)
        {
            if (!grid.TryGetPosition(context.SelfGuid, out var self)) return null;
            if (Coords == null) return null;

            var absolute = new List<GridCoord>(Coords.Count);
            foreach (var offset in Coords) absolute.Add(self + offset);
            return PlaceableSubset(absolute, grid);
        }

        private List<GridCoord> RowSlots(AIContext context, IGridManager grid)
        {
            if (!grid.TryGetPosition(context.SelfGuid, out var self)) return null;

            var sides = Side == RowSide.Auto
                ? new[] { new GridCoord(0, -1), new GridCoord(0, 1), new GridCoord(-1, 0), new GridCoord(1, 0) }
                : new[] { OffsetOf(Side) };

            List<GridCoord> best = null;
            foreach (var offset in sides)
            {
                var row = PlaceableSubset(RowFor(self, offset), grid);
                if (best == null || row.Count > best.Count) best = row;
            }
            return best;
        }

        /// <summary>
        /// Fila de <see cref="Count"/> casillas centrada un paso hacia <paramref name="offset"/>. Crece
        /// perpendicular al offset: a lo ancho de la pared, no hacia el jugador. Que arranque en el
        /// anillo del jefe es lo que deja su melee cubriendo las casillas junto al objeto del medio.
        /// </summary>
        private List<GridCoord> RowFor(GridCoord self, GridCoord offset)
        {
            var step = new GridCoord(offset.Y, offset.X);
            var center = self + offset;

            var row = new List<GridCoord>(Count);
            int half = Count / 2;
            for (int i = 0; i < Count; i++)
            {
                int k = i - half;
                row.Add(new GridCoord(center.X + step.X * k, center.Y + step.Y * k));
            }
            return row;
        }

        private static GridCoord OffsetOf(RowSide side) => side switch
        {
            RowSide.Up => new GridCoord(0, 1),
            RowSide.Left => new GridCoord(-1, 0),
            RowSide.Right => new GridCoord(1, 0),
            _ => new GridCoord(0, -1),
        };

        /// <summary>
        /// Anillos concéntricos alrededor del jefe, de adentro hacia afuera, hasta juntar
        /// <paramref name="budget"/> (default <see cref="Count"/>). Abrirse en radio en vez de caer a
        /// casillas sueltas de la sala es lo que mantiene los objetos leyendo como suyos.
        /// </summary>
        /// <remarks>
        /// El parámetro es lo que le permite a <see cref="Placement.DoorFronts"/> pedirle sólo el
        /// remanente tras repartir en las puertas, sin tocar <see cref="Count"/> — es data de autoría
        /// serializada, mutarla acá correría el patrón entero.
        /// </remarks>
        private List<GridCoord> RingSlots(AIContext context, IGridManager grid, int? budget = null)
        {
            int cap = budget ?? Count;
            if (!grid.TryGetPosition(context.SelfGuid, out var self)) return null;

            var result = new List<GridCoord>(cap);
            for (int radius = 1; radius <= MaxRingRadius && result.Count < cap; radius++)
            {
                foreach (var c in Ring(self, radius))
                {
                    if (result.Count >= cap) break;
                    if (!IsPlaceable(grid, c) || result.Contains(c)) continue;
                    result.Add(c);
                }
            }
            return result;
        }

        private static IEnumerable<GridCoord> Ring(GridCoord center, int radius)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                    yield return new GridCoord(center.X + dx, center.Y + dy);
                }
            }
        }

        /// <summary>
        /// Frente de cada puerta autorada de la sala (orden de <see cref="RoomLayout.DoorSlots"/>),
        /// y el remanente de <see cref="Count"/> repartido en anillo alrededor del jefe.
        /// </summary>
        /// <remarks>
        /// El remanente al anillo es lo que hace que un <c>Count</c> mayor a la cantidad de puertas
        /// degrade bien: sin puertas resueltas (sala sin <c>RoomLayout</c>, o sin
        /// <see cref="IDungeonService"/> — el caso de los fixtures de EditMode) el resultado es el
        /// mismo que pedir <see cref="Placement.RingAroundSelf"/> directamente.
        /// </remarks>
        private List<GridCoord> DoorFrontSlots(AIContext context, IGridManager grid)
            => BuildDoorFrontSlots(ResolveDoorFrontCoords(grid), context, grid);

        /// <summary>
        /// Casillas interiores frente a los slots de <see cref="RoomLayout.DoorSlots"/> — TODOS los
        /// autorados, sin filtrar por estado. <see cref="DoorTileQuery.GetOpenDoorFrontTiles"/>
        /// no sirve acá: en un jefe la sala está <c>Uncleared</c> durante la pelea, ninguna puerta
        /// está <c>Open</c> (entrada/salida en <c>LockedCombat</c>, las perpendiculares <c>Tapiada</c>)
        /// y esa query devolvería vacío siempre. <c>DungeonManager.ConfigureDoorSlots</c> ya
        /// fuerza-activa cada <c>DoorRoot</c> antes del chequeo de conexión, así que los cuatro
        /// anchors están resueltos igual.
        /// </summary>
        private static IEnumerable<GridCoord> ResolveDoorFrontCoords(IGridManager grid)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null)
                yield break;

            var prefab = dungeon.CurrentRoomInstance?.SpawnedPrefab;
            if (prefab == null) yield break;

            var layout = prefab.GetComponent<RoomLayout>();
            if (layout == null || layout.DoorSlots == null) yield break;

            foreach (var slot in layout.DoorSlots)
            {
                if (slot?.Anchor == null) continue;

                // Misma resolución que DoorTileQuery / PlayerRoomTransitioner.ResolveSpawnCoord: un
                // paso hacia adentro desde el anchor cae en la primera celda interior.
                yield return grid.WorldToGrid(slot.Anchor.position) + slot.Direction.InwardOffset();
            }
        }

        /// <summary>
        /// Seam testeable en EditMode del merge de <see cref="Placement.DoorFronts"/>: leer el
        /// <see cref="RoomLayout"/> necesita un prefab de sala instanciado, pero el orden/presupuesto/
        /// dedupe de acá no depende del engine y es la parte que puede romperse en silencio.
        /// </summary>
        internal List<GridCoord> BuildDoorFrontSlots(
            IEnumerable<GridCoord> doorFronts, AIContext context, IGridManager grid)
        {
            var fromDoors = PlaceableSubset(doorFronts, grid);

            var result = new List<GridCoord>(Count);
            foreach (var c in fromDoors)
            {
                if (result.Count >= Count) break;
                result.Add(c);
            }

            int remaining = Count - result.Count;
            if (remaining > 0)
            {
                var ring = RingSlots(context, grid, remaining);
                if (ring != null)
                {
                    foreach (var c in ring)
                    {
                        if (result.Count >= Count) break;
                        if (result.Contains(c)) continue;
                        result.Add(c);
                    }
                }
            }

            return result;
        }

        /// <remarks>
        /// Con <see cref="MinSpacing"/> el sorteo es goloso: cada candidata elegida poda del pool
        /// todo lo que le quede cerca (y de paso, la propia casilla del jefe se poda antes de
        /// arrancar). Si el pool se seca antes de juntar <see cref="Count"/>, se devuelve lo que
        /// haya — mismo degradado silencioso que sin separación.
        /// </remarks>
        private List<GridCoord> ScatteredSlots(AIContext context, IGridManager grid)
        {
            var graph = grid.Graph;
            if (graph == null || graph.IsEmpty) return null;

            bool hasSelf = grid.TryGetPosition(context.SelfGuid, out var self);

            var pool = new List<GridCoord>();
            foreach (var c in graph.AllCoords())
            {
                if (!IsPlaceable(grid, c)) continue;
                if (MinSpacing > 0 && hasSelf && c.Chebyshev(self) < MinSpacing) continue;
                pool.Add(c);
            }

            var rng = context.Rng ?? new System.Random();
            var result = new List<GridCoord>(Count);
            while (result.Count < Count && pool.Count > 0)
            {
                int idx = rng.Next(pool.Count);
                var picked = pool[idx];
                pool.RemoveAt(idx);
                result.Add(picked);

                if (MinSpacing > 0) pool.RemoveAll(c => c.Chebyshev(picked) < MinSpacing);
            }
            return result;
        }

        /// <summary>
        /// Si el objeto entra en la casilla. La ocupancia sólo importa cuando bloquea: sin
        /// <see cref="RoomObjectDefinitionSO.Blocks"/> el objeto no entra al mapa de ocupancia, así
        /// que compartir casilla con el jugador es legal.
        /// </summary>
        private bool IsPlaceable(IGridManager grid, GridCoord coord)
        {
            if (!grid.InBounds(coord) || !grid.IsWalkable(coord)) return false;
            return !Definition.Blocks || !grid.IsOccupied(coord);
        }
    }
}
