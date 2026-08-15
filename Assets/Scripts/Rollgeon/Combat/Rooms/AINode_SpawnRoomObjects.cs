using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities.Portraits;
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
    /// <b>El nodo no sabe qué objeto es.</b> Mismo criterio que <c>AINode_ActivateHazard</c>: acá no
    /// hay rodillos ni dados, hay una definición y una forma. Un jefe nuevo con objetos propios apunta
    /// este nodo a otro <c>.asset</c> — cero código.
    /// </para>
    /// <para>
    /// <b>NO envolver en <c>Once</c>.</b> El nodo se auto-gatea (coloca las ranuras una sola vez) pero
    /// necesita tickear cada turno del jefe para correr los relojes de reposición. Envuelto en
    /// <c>Once</c> queda latcheado tras el primer spawn y ningún objeto vuelve nunca — el mismo
    /// accidente que ya documenta <c>SunkenGrandPhaseWiringTests</c> para los refuerzos. Devuelve
    /// <see cref="AIResult.Succeeded"/> en los ticks de espera para no abortar el Sequence del jefe.
    /// </para>
    /// <para>
    /// <b>Orden interno del tick</b>: recoger rotos → correr relojes → reponer. Las casillas se
    /// resuelven una vez y se recuerdan: que el objeto vuelva donde estaba es lo que hace legible la
    /// pinza (sabés dónde reaparece el que rompiste).
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SpawnRoomObjects : AIActionNode
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

        [Tooltip("Casillas autoradas: absolutas con Pattern = ExplicitCoords, relativas al jefe con " +
                 "Pattern = OffsetsFromSelf. Ignorado por el resto de los patrones.")]
        public List<GridCoord> Coords = new List<GridCoord>();

        /// <summary>Anillos que <see cref="Placement.RingAroundSelf"/> llega a abrir antes de rendirse.
        /// Más lejos que esto el objeto ya no lee como "del jefe" y conviene otro patrón.</summary>
        private const int MaxRingRadius = 4;

        // --- Runtime state (per-combat). NonSerialized: vive solo en la copia runtime del árbol
        // (EnemyDataSO.CreateRuntimeAIRoot → SerializationUtility.CreateCopy), nunca en el asset.
        // Mismo patrón que AINode_SpawnReinforcements: una pelea nueva arranca con _slots en null
        // ⇒ las ranuras se re-resuelven contra la sala nueva.
        [NonSerialized] private List<Slot> _slots;

        private sealed class Slot
        {
            public GridCoord Coord;

            /// <summary><see cref="Guid.Empty"/> = ranura vacía (nunca llenada, o rota).</summary>
            public Guid ObjectGuid;

            public int TurnsUntilRespawn;

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

        public override AIResult Tick(AIContext context)
        {
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
            return AIResult.Succeeded;
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
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Retired || slot.ObjectGuid != Guid.Empty) continue;

                if (slot.TurnsUntilRespawn > 0)
                {
                    slot.TurnsUntilRespawn--;
                    continue;
                }

                // Ranura pisada (el jugador parado ahí) o sin piso: se reintenta el próximo turno. El
                // reloj ya está en 0, así que la espera no acumula deuda.
                if (!IsPlaceable(grid, slot.Coord)) continue;

                slot.ObjectGuid = Spawn(context, grid, slot.Coord);
            }
        }

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
        /// <see cref="Count"/>. Abrirse en radio en vez de caer a casillas sueltas de la sala es lo
        /// que mantiene los objetos leyendo como suyos.
        /// </summary>
        private List<GridCoord> RingSlots(AIContext context, IGridManager grid)
        {
            if (!grid.TryGetPosition(context.SelfGuid, out var self)) return null;

            var result = new List<GridCoord>(Count);
            for (int radius = 1; radius <= MaxRingRadius && result.Count < Count; radius++)
            {
                foreach (var c in Ring(self, radius))
                {
                    if (result.Count >= Count) break;
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

        private List<GridCoord> ScatteredSlots(AIContext context, IGridManager grid)
        {
            var graph = grid.Graph;
            if (graph == null || graph.IsEmpty) return null;

            var pool = new List<GridCoord>();
            foreach (var c in graph.AllCoords())
            {
                if (IsPlaceable(grid, c)) pool.Add(c);
            }

            var rng = context.Rng ?? new System.Random();
            var result = new List<GridCoord>(Count);
            while (result.Count < Count && pool.Count > 0)
            {
                int idx = rng.Next(pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
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
