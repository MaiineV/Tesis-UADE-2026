using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.Rooms
{
    /// <summary>
    /// Siembra bombas por la sala, cada una con su propia cruz avisada (su casilla + las 4
    /// ortogonales, recortadas contra la sala) y su mecha corriendo. Envuelve un
    /// <see cref="AINode_SpawnRoomObjects"/> configurado en
    /// <see cref="AINode_SpawnRoomObjects.Placement.ScatteredFree"/> — no lo reautora, lo arma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sólo siembra.</b> El estallido es <see cref="AINode_DetonateBombField"/>, que va fuera del
    /// ciclo y descuenta la mecha todos los turnos. Están partidos porque con las dos cosas en el
    /// mismo tick la mecha vale siempre el intervalo con el que se tickea el nodo —un ciclo entero—,
    /// y no hay forma de expresar un plazo más corto. El estado compartido vive en
    /// <see cref="IBombFieldService"/>.
    /// </para>
    /// <para>
    /// La cruz de cada bomba se guarda por guid, no por casilla: es lo que hace que romper UNA no
    /// toque las demás. El servicio de amenaza (<see cref="IThreatenedAreaService"/>) sólo entiende
    /// de fuentes, así que cada bomba usa la suya propia (<see cref="ChannelPrefix"/> + su guid) —
    /// mismo truco de canal que <see cref="AINode_TelegraphMark"/>.
    /// </para>
    /// <para>
    /// <see cref="Definition"/> tiene que traer <c>RespawnDelayTurns = 0</c>: es lo que deja que la
    /// siembra reponga en la misma pasada tanto lo que detonó como lo que el jugador rompió a mano.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_BombField : AIActionNode
    {
        [Tooltip("La bomba a sembrar. RespawnDelayTurns tiene que ser 0: es lo que permite que la " +
                 "siembra reponga tanto lo detonado como lo roto a mano.")]
        public RoomObjectDefinitionSO Definition;

        [MinValue(1)]
        [Tooltip("Cantidad de bombas por siembra.")]
        public int Count = 4;

        [MinValue(0)]
        [Tooltip("Separación mínima entre bombas y contra el jefe, en Chebyshev. Con menos de 3 dos " +
                 "cruces alineadas comparten la casilla del medio y las dos bombas se leen como una " +
                 "mancha; a 3 no se tocan nunca.")]
        public int Spacing = 3;

        [MinValue(1)]
        [Tooltip("Turnos que la bomba está en pie antes de estallar — o sea, cuántas acciones tiene " +
                 "el jugador para romperla. Los descuenta AINode_DetonateBombField.")]
        public int FuseTurns = 2;

        [MinValue(0)]
        [Tooltip("Daño que se declara en la marca de la cruz. Lo cobra el nodo que detona.")]
        public int IgnitionDamage;

        [Tooltip("Prefijo del canal de amenaza por bomba (prefijo + guid). Tiene que ser el MISMO " +
                 "que el de AINode_DetonateBombField.")]
        public string ChannelPrefix = "bomb.";

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Gesto del jefe al sembrar. Va al AINode_SpawnRoomObjects de adentro, así que las " +
                 "bombas caen con su animación en vez de materializarse mientras él sigue en idle.")]
        public string SowFeedbackId;

        [NonSerialized] private AINode_SpawnRoomObjects _spawner;

        public override string NodeName =>
            $"Bomb Field ({Count}x {(Definition != null ? Definition.name : "?")})";

        /// <summary>Las bombas en pie con su cruz. Sale del servicio, que filtra por vida actual.</summary>
        public static IEnumerable<(Guid Guid, IReadOnlyList<GridCoord> Cross)> LiveCrosses(
            AttributesManager attributes) =>
            BombFieldService.ResolveOrCreate().Live(attributes);

        /// <summary>
        /// Canal de amenaza de una bomba. Estático y compartido con el nodo que detona: es lo único
        /// que los dos lados tienen que derivar igual, y derivarlo mal levanta cruces que nadie pintó.
        /// </summary>
        public static Guid ChannelFor(Guid selfGuid, string channelPrefix, Guid bombGuid) =>
            AINode_TelegraphMark.SourceKey(selfGuid, channelPrefix + bombGuid.ToString("N"));

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            if (Definition == null)
            {
                Debug.LogWarning("[AINode_BombField] Falta Definition — no se siembra nada.");
                return AIResult.Succeeded;
            }

            var grid = context.Grid;
            if (grid == null || context.Attributes == null) return AIResult.Failed;

            var spawner = EnsureSpawner();
            spawner.Tick(context);

            MarkNewBombs(context, grid, spawner);

            return AIResult.Succeeded;
        }

        /// <remarks>
        /// La siembra va por el <c>TickCoroutine</c> del spawner y no por su <c>Tick</c>: es el que
        /// toca el gesto de <see cref="SowFeedbackId"/>, y sin eso las bombas nuevas aparecen solas.
        /// </remarks>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (context == null || Definition == null || context.Grid == null || context.Attributes == null)
            {
                onResult?.Invoke(Tick(context));
                yield break;
            }

            var spawner = EnsureSpawner();
            var sow = spawner.TickCoroutine(context, null);
            while (sow.MoveNext()) yield return sow.Current;

            MarkNewBombs(context, context.Grid, spawner);

            onResult?.Invoke(AIResult.Succeeded);
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
                SpawnFeedbackId = SowFeedbackId,
            };
        }

        /// <remarks>
        /// <para>
        /// Pasa por TODAS las que están en pie y no sólo por las nuevas, y eso está bien: volver a
        /// marcar la cruz de una que ya estaba es idempotente, y <c>Sow</c> no le refresca la mecha
        /// a una bomba ya armada.
        /// </para>
        /// <para>
        /// El overlay va por <c>ResolveOrCreate</c> y no por <c>TryGetService</c>: no está en los
        /// bootstrap, lo crea el primero que pinta. Consultándolo, la primera siembra de la pelea
        /// caía antes de que existiera y esas bombas quedaban sin cruz.
        /// </para>
        /// </remarks>
        private void MarkNewBombs(AIContext context, IGridManager grid, AINode_SpawnRoomObjects spawner)
        {
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat);
            var overlay = ThreatTelegraphOverlay.ResolveOrCreate();

            var field = BombFieldService.ResolveOrCreate();

            foreach (var (guid, coord) in spawner.LiveObjects())
            {
                var cross = ComputeCross(coord, grid);
                field.Sow(guid, cross, FuseTurns);

                var channel = ChannelFor(context.SelfGuid, ChannelPrefix, guid);
                threat?.Mark(channel, cross, IgnitionDamage, AttackKind.Environmental);
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
    }
}
