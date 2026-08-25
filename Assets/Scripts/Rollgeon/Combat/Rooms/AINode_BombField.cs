using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.Rooms
{
    /// <summary>
    /// El tiempo del medio del ciclo del Croupier: siembra bombas por la sala, cada una con su
    /// propia cruz avisada (su casilla + las 4 ortogonales, recortadas contra la sala). La que
    /// sigue en pie un ciclo entero después detona sola. Envuelve un
    /// <see cref="AINode_SpawnRoomObjects"/> configurado en
    /// <see cref="AINode_SpawnRoomObjects.Placement.ScatteredFree"/> — no lo reautora, lo arma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El nodo asume que el árbol del jefe lo tickea una vez por ciclo (cada 3 turnos): cada
    /// <see cref="Tick"/> hace, en orden, "detonar lo que sobrevivió → sembrar de nuevo → marcar lo
    /// nuevo" — así entre que una bomba aparece y detona pasan exactamente los tres turnos del
    /// ciclo del jefe, sin que este nodo lleve su propio contador.
    /// </para>
    /// <para>
    /// La cruz de cada bomba se guarda por guid, no por casilla: es lo que hace que romper UNA no
    /// toque las demás. El servicio de amenaza (<see cref="IThreatenedAreaService"/>) sólo entiende
    /// de fuentes, así que cada bomba usa la suya propia (<see cref="ChannelPrefix"/> + su guid) —
    /// mismo truco de canal que <see cref="AINode_TelegraphMark"/>.
    /// </para>
    /// <para>
    /// La vida es la autoridad, no la marca del servicio: si el jugador ya rompió la bomba a mitad
    /// de ciclo, su <see cref="Health"/> llega a 0 antes de que este nodo vuelva a tickear, y ese
    /// chequeo — no un evento — es lo que decide si detona. Por eso <see cref="Definition"/> tiene
    /// que traer <c>RespawnDelayTurns = 0</c>: es lo que deja que el "sembrar de nuevo" de este
    /// mismo tick repare tanto lo detonado como lo roto a mano, en la misma pasada.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_BombField : AIActionNode
    {
        [Tooltip("La bomba a sembrar. RespawnDelayTurns tiene que ser 0: es lo que permite que la " +
                 "siembra de este mismo tick repare tanto lo detonado como lo roto a mano.")]
        public RoomObjectDefinitionSO Definition;

        [Tooltip("Casilla especial que deja la detonación (fuego). Romper la bomba a mano NO la " +
                 "planta — sólo el plazo cumplido lo hace.")]
        public SpecialTileDefinitionSO FireTile;

        [MinValue(1)]
        [Tooltip("Cantidad de bombas por ciclo.")]
        public int Count = 5;

        [MinValue(0)]
        [Tooltip("Separación mínima entre bombas y contra el jefe, en Chebyshev. Con menos de 3 dos " +
                 "cruces alineadas comparten la casilla del medio y las dos bombas se leen como una " +
                 "mancha; a 3 no se tocan nunca.")]
        public int Spacing = 3;

        [MinValue(0)]
        [Tooltip("Rondas que arde el fuego de la detonación. 0 = usa el default del SO de FireTile.")]
        public int FireDurationRounds;

        [MinValue(0)]
        [Tooltip("Daño de la detonación a quien siga parado en la cruz cuando prende.")]
        public int IgnitionDamage = 20;

        [Tooltip("Prefijo del canal de amenaza por bomba (prefijo + guid). Sólo importa si el mismo " +
                 "jefe usa AINode_BombField más de una vez con canales que puedan chocar.")]
        public string ChannelPrefix = "bomb.";

        [NonSerialized] private AINode_SpawnRoomObjects _spawner;
        [NonSerialized] private Dictionary<Guid, List<GridCoord>> _crossByGuid;

        public override string NodeName =>
            $"Bomb Field ({Count}x {(Definition != null ? Definition.name : "?")})";

        /// <summary>
        /// Cruces todavía armadas: filtra por vida ACTUAL, no por lo que el último tick marcó — así
        /// una bomba rota a mano deja de listarse antes de que este nodo vuelva a tickear.
        /// </summary>
        public IEnumerable<(Guid Guid, IReadOnlyList<GridCoord> Cross)> LiveCrosses(AttributesManager attributes)
        {
            if (_crossByGuid == null) yield break;

            foreach (var kvp in _crossByGuid)
            {
                var health = attributes?.GetAttribute<Health>(kvp.Key);
                if (health == null || health.Value <= 0) continue;
                yield return (kvp.Key, kvp.Value);
            }
        }

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            if (Definition == null || FireTile == null)
            {
                Debug.LogWarning("[AINode_BombField] Falta Definition o FireTile — no se siembra " +
                                 "ni detona nada.");
                return AIResult.Succeeded;
            }

            var grid = context.Grid;
            if (grid == null || context.Attributes == null) return AIResult.Failed;

            _crossByGuid ??= new Dictionary<Guid, List<GridCoord>>();

            DetonateSurvivors(context, grid);

            var spawner = EnsureSpawner();
            spawner.Tick(context);

            MarkNewBombs(context, grid, spawner);

            return AIResult.Succeeded;
        }

        private AINode_SpawnRoomObjects EnsureSpawner()
        {
            return _spawner ??= new AINode_SpawnRoomObjects
            {
                Definition = Definition,
                Count = Count,
                Pattern = AINode_SpawnRoomObjects.Placement.ScatteredFree,
                MinSpacing = Spacing,
                ResolveSlotsEachSpawn = true,
            };
        }

        private void DetonateSurvivors(AIContext context, IGridManager grid)
        {
            if (_crossByGuid.Count == 0) return;

            ServiceLocator.TryGetService<ISpecialTileService>(out var special);
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat);
            ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay);

            foreach (var kvp in _crossByGuid)
            {
                var guid = kvp.Key;
                var cross = kvp.Value;

                var health = context.Attributes.GetAttribute<Health>(guid);
                if (health != null && health.Value > 0) Detonate(context, grid, special, guid, cross);

                var channel = ChannelFor(context.SelfGuid, guid);
                threat?.Clear(channel);
                overlay?.Clear(channel);
            }

            _crossByGuid.Clear();
        }

        private void Detonate(
            AIContext context, IGridManager grid, ISpecialTileService special, Guid guid, List<GridCoord> cross)
        {
            special?.Place(FireTile, cross, new TilePlacementOptions
            {
                Owner = context.SelfGuid,
                DurationRounds = FireDurationRounds > 0 ? FireDurationRounds : 0,
            });

            ChargeIgnitionDamage(context, cross);

            context.VisualService?.Despawn(guid);
            grid.Unregister(guid);

            // El spawner de abajo recién nota la rotura en su PROPIO Tick (CollectBroken mira
            // Health), y esta detonación pasa por afuera de esa vía — sin esto la ranura le queda
            // viva a sus ojos y nunca se resiembra.
            context.Attributes.SetAttributeValue<Health, int>(guid, 0);
        }

        private void ChargeIgnitionDamage(AIContext context, List<GridCoord> cross)
        {
            if (IgnitionDamage <= 0 || context.DamagePipeline == null) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return;
            if (!cross.Contains(playerCoord)) return;

            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = IgnitionDamage,
                Kind = AttackKind.Environmental,
            });
        }

        private void MarkNewBombs(AIContext context, IGridManager grid, AINode_SpawnRoomObjects spawner)
        {
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat);
            ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay);

            foreach (var (guid, coord) in spawner.LiveObjects())
            {
                var cross = ComputeCross(coord, grid);
                _crossByGuid[guid] = cross;

                if (threat == null) continue;

                var channel = ChannelFor(context.SelfGuid, guid);
                threat.Mark(channel, cross, IgnitionDamage, AttackKind.Environmental);
                overlay?.Show(channel, cross);
            }
        }

        private static List<GridCoord> ComputeCross(GridCoord center, IGridManager grid)
        {
            var cross = new List<GridCoord>(5);
            if (grid.InBounds(center) && grid.IsWalkable(center)) cross.Add(center);

            foreach (var n in center.Neighbors4())
                if (grid.InBounds(n) && grid.IsWalkable(n)) cross.Add(n);

            return cross;
        }

        private Guid ChannelFor(Guid selfGuid, Guid bombGuid) =>
            AINode_TelegraphMark.SourceKey(selfGuid, ChannelPrefix + bombGuid.ToString("N"));
    }
}
