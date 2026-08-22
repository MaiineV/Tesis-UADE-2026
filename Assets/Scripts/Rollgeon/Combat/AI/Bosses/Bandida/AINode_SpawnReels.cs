using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Initiative;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
using Rollgeon.Entities.Portraits;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Mantiene la fila de rodillos de La Bandida: la arma alineada en el primer turno, detecta los
    /// rotos, deja su casilla en llamas, los repone a los <see cref="RespawnDelayTurns"/> turnos del
    /// jefe en su ranura original y rearma la cuenta del jackpot en el mismo paso en que devuelve un
    /// rodillo.
    /// </summary>
    /// <remarks>
    /// El nodo se auto-gatea (arma la fila una sola vez) pero necesita tickear cada turno del jefe
    /// para correr los relojes de reposición: envuelto en un <c>Once</c> queda latcheado tras el
    /// primer spawn y ningún rodillo vuelve. Orden interno del tick: detectar rotos → correr
    /// relojes → reponer.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SpawnReels : AIActionNode
    {
        /// <summary>Dirección de la fila respecto del jefe.</summary>
        public enum RowDirection
        {
            /// <summary>Elige el lado con más tiles válidos — el que no da a la pared.</summary>
            Auto = 0,
            Down = 1,
            Up = 2,
            Left = 3,
            Right = 4,
        }

        [OdinSerialize]
        [Tooltip("EnemyDataSO del rodillo: el objeto de 60 HP que no actúa. A esa vida romper uno " +
                 "cuesta casi un turno entero de daño, que es lo que vuelve la elección una decisión.")]
        public EnemyDataSO ReelData;

        [OdinSerialize]
        [Tooltip("Hazard que queda sobre la casilla del rodillo roto. Es el fuego de paño del " +
                 "Croupier reusado tal cual (6 por terminar el turno adentro, 2 rondas): la sustancia " +
                 "ya existe y duplicarla sería dos assets que se desincronizan. Vacío = romper un " +
                 "rodillo deja piso limpio.")]
        public HazardDefinitionSO OnBreakHazard;

        [Tooltip("Rodillos en la fila. 3 = el diseño de La Bandida (el del medio es el que se traba).")]
        [MinValue(1)]
        public int Count = 3;

        [Tooltip("Turnos del jefe que tarda un rodillo roto en volver. Fase 2 lo baja a 1 vía " +
                 "AINode_SetReelRespawnDelay.")]
        [MinValue(0)]
        public int RespawnDelayTurns = 2;

        [Tooltip("Valor con el que arranca la cuenta cada vez que un rodillo vuelve a la fila.")]
        [MinValue(0)]
        public int CountdownOnRespawn = 2;

        [Tooltip("Lado del jefe donde se alinea la fila. Auto = el lado con más tiles libres.")]
        public RowDirection Direction = RowDirection.Auto;

        public override string NodeName =>
            $"Spawn Reels ({Count}x {(ReelData != null ? ReelData.name : "?")}, respawn {RespawnDelayTurns})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || ReelData == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null || context.Attributes == null) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<InMemoryEntityRegistry>(out var registry) || registry == null)
                return AIResult.Failed;
            if (!ServiceLocator.TryGetService<TurnOrderService>(out var turnOrder) || turnOrder == null)
                return AIResult.Failed;

            var service = BandidaJackpotService.ResolveOrCreate();
            service.BindBoss(context.SelfGuid);
            service.InitRespawnDelay(RespawnDelayTurns);

            if (service.Slots.Count == 0)
            {
                var row = BuildRow(grid, context.SelfGuid);
                if (row == null) return AIResult.Failed;
                service.SetSlots(row);
            }

            MarkBrokenReels(service, context.Attributes);
            bool anyReturned = RunRespawnClocks(service, context, grid, registry, turnOrder);

            // Reponer y rearmar la cuenta son el MISMO paso: si no se resetea acá, el rodillo vuelve
            // alineado con la cuenta vieja y el jackpot dispara al turno siguiente del respawn.
            if (anyReturned) service.ResetCountdown(CountdownOnRespawn);

            return AIResult.Succeeded;
        }

        // ======================================================================
        // Ranuras
        // ======================================================================

        /// <summary>
        /// Tiles de la fila: <see cref="Count"/> casillas consecutivas centradas en la coordenada
        /// del jefe, un paso hacia el lado elegido. <c>null</c> si ningún lado tiene tiles.
        /// </summary>
        private List<GridCoord> BuildRow(IGridManager grid, Guid selfGuid)
        {
            if (!grid.TryGetPosition(selfGuid, out var bossCoord)) return null;

            var candidates = Direction == RowDirection.Auto
                ? new[] { new GridCoord(0, -1), new GridCoord(0, 1), new GridCoord(-1, 0), new GridCoord(1, 0) }
                : new[] { OffsetOf(Direction) };

            List<GridCoord> best = null;
            int bestValid = 0;

            foreach (var offset in candidates)
            {
                var row = RowFor(bossCoord, offset);
                int valid = 0;
                foreach (var c in row)
                {
                    if (grid.InBounds(c) && grid.IsWalkable(c)) valid++;
                }

                if (valid > bestValid)
                {
                    bestValid = valid;
                    best = row;
                }
            }

            return bestValid > 0 ? best : null;
        }

        private List<GridCoord> RowFor(GridCoord bossCoord, GridCoord offset)
        {
            // Perpendicular al offset: la fila crece a lo ancho de la pared, no hacia el jugador.
            var step = new GridCoord(offset.Y, offset.X);
            var center = bossCoord + offset;

            var row = new List<GridCoord>(Count);
            int half = Count / 2;
            for (int i = 0; i < Count; i++)
            {
                int k = i - half;
                row.Add(new GridCoord(center.X + step.X * k, center.Y + step.Y * k));
            }
            return row;
        }

        private static GridCoord OffsetOf(RowDirection dir) => dir switch
        {
            RowDirection.Up => new GridCoord(0, 1),
            RowDirection.Left => new GridCoord(-1, 0),
            RowDirection.Right => new GridCoord(1, 0),
            _ => new GridCoord(0, -1),
        };

        // ======================================================================
        // Ciclo de rotura / reposición
        // ======================================================================

        /// <summary>
        /// Pasa a "roto" toda ranura cuyo rodillo ya no tenga vida y prende su casilla. No hay
        /// evento de objeto destruido al que colgarse: la rotura se descubre comparando vidas.
        /// </summary>
        private void MarkBrokenReels(IBandidaJackpotService service, AttributesManager attrs)
        {
            var slots = service.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsAlive) continue;

                var health = attrs.GetAttribute<Health>(slots[i].ReelGuid);
                if (health != null && health.Value > 0) continue;

                var coord = slots[i].Coord;
                service.DetachReel(i);
                IgniteBrokenSlot(coord);
            }
        }

        /// <summary>
        /// Deja el fuego sobre la casilla que ocupaba el rodillo. Una instancia por rotura, para que
        /// dos rodillos rotos sean dos llamas con su propia duración.
        /// </summary>
        /// <remarks>
        /// Overload de tiles y no el de definición: la forma autorada en el asset es un sector
        /// entero y acá el fuego es exactamente una casilla.
        /// </remarks>
        private void IgniteBrokenSlot(GridCoord coord)
        {
            if (OnBreakHazard == null) return;
            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null) return;

            hazards.Activate(OnBreakHazard, new[] { coord });
        }

        /// <summary>
        /// Corre el reloj de cada ranura vacía y repone las que llegaron a 0. Devuelve <c>true</c>
        /// si al menos un rodillo volvió a la fila este turno.
        /// </summary>
        private bool RunRespawnClocks(IBandidaJackpotService service, AIContext context,
            IGridManager grid, InMemoryEntityRegistry registry, TurnOrderService turnOrder)
        {
            bool anyReturned = false;
            var slots = service.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsAlive) continue;

                if (slot.TurnsUntilRespawn > 0)
                {
                    slot.TurnsUntilRespawn--;
                    if (slot.TurnsUntilRespawn > 0) continue;
                }

                // Reloj cumplido. Si la ranura está pisada (el jugador parado ahí) se reintenta el
                // próximo turno: el reloj ya está en 0, así que no se acumula deuda.
                if (!grid.InBounds(slot.Coord) || !grid.IsWalkable(slot.Coord) || grid.IsOccupied(slot.Coord))
                    continue;

                var reelGuid = SpawnReel(context, grid, registry, turnOrder, slot.Coord);
                if (reelGuid == Guid.Empty) continue;

                service.AttachReel(i, reelGuid);
                if (slot.Locked) ApplyHold(context.Attributes, reelGuid, service.LockedReelHp);
                anyReturned = true;
            }

            return anyReturned;
        }

        /// <summary>
        /// HOLD: el rodillo trabado vuelve con un pool de vida inagotable (el
        /// <c>DamagePipeline</c> no tiene canal de inmunidad).
        /// </summary>
        private static void ApplyHold(AttributesManager attrs, Guid reelGuid, int lockedHp)
        {
            if (attrs == null || lockedHp <= 0) return;
            attrs.SetAttributeValue<Health, int>(reelGuid, lockedHp);
        }

        /// <summary>
        /// Spawnea un rodillo. Entra a la cola de turnos aunque su árbol sea un no-op: es lo que
        /// hace que <c>CombatDeathWatcher</c> lo limpie al morir el jefe.
        /// </summary>
        private Guid SpawnReel(AIContext context, IGridManager grid, InMemoryEntityRegistry registry,
            TurnOrderService turnOrder, GridCoord coord)
        {
            ServiceLocator.TryGetService<IEnemyAIRegistry>(out var aiRegistry);
            ServiceLocator.TryGetService<IEntityPortraitResolver>(out var portraits);
            var visuals = context.VisualService;

            const int tier = 1;
            var id = Guid.NewGuid();
            var attrs = ReelData.CreateRuntimeStats(tier);

            registry.Register(id, attrs);
            context.Attributes.Register(id, attrs);
            portraits?.Register(id, ReelData.Portrait);

            if (aiRegistry != null)
                aiRegistry.Register(id, ReelData.CreateRuntimeAIRoot(), ReelData.ResolveMaxHP(tier));

            grid.Register(id, coord);
            visuals?.SpawnEnemy(id, ReelData, coord);

            if (visuals != null && visuals.TryGetPawn(id, out var pawn) && pawn.HealthBar != null)
            {
                int maxHp = ReelData.ResolveMaxHP(tier);
                pawn.HealthBar.Initialize(id, maxHp, maxHp);
            }

            turnOrder.Append(id);

            // Mismo diferido que un refuerzo: aparece en la ronda en curso sin actuar.
            EventManager.Trigger(EventName.OnReinforcementSpawned, id);

            return id;
        }
    }
}
