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
    /// Siembra bombas por la sala, cada una con su propia cruz avisada (su casilla + los 4 brazos
    /// que le da <see cref="Shape"/>, recortados contra la sala) y su mecha corriendo. Envuelve un
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
    public sealed class AINode_BombField : AIActionNode, IAIIntentNode
    {
        [Tooltip("La bomba a sembrar. RespawnDelayTurns tiene que ser 0: es lo que permite que la " +
                 "siembra reponga tanto lo detonado como lo roto a mano.")]
        public RoomObjectDefinitionSO Definition;

        /// <summary>
        /// El dibujo de la cruz. Los dos tienen 5 casillas, así que el fuego que queda pesa lo mismo:
        /// lo que cambia es <b>dónde está el hueco seguro</b>. En <see cref="Orthogonal"/> se salva
        /// la diagonal, en <see cref="Diagonal"/> se salva la ortogonal.
        /// </summary>
        public enum BlastShape
        {
            /// <summary>La de siempre: <c>+</c>, la casilla y sus 4 vecinas ortogonales.</summary>
            Orthogonal = 0,

            /// <summary>El aspa: <c>×</c>, la casilla y sus 4 diagonales.</summary>
            Diagonal = 1,

            /// <summary>
            /// Una y otra, cambiando en cada siembra y arrancando por <see cref="Orthogonal"/>. La
            /// esquiva no se puede memorizar: la casilla que salvó de la generación anterior es
            /// justo la que mata en la siguiente.
            /// </summary>
            Alternating = 2,
        }

        [MinValue(1)]
        [Tooltip("Cantidad de bombas por siembra.")]
        public int Count = 4;

        [Tooltip("Forma de la cruz. Alternating rota + y × en cada siembra: las dos cubren 5 " +
                 "casillas, pero el hueco seguro se invierte.")]
        public BlastShape Shape = BlastShape.Orthogonal;

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

        [Tooltip("Deja la cruz marcada pero NO la pinta: el dibujo sale sólo al pasar el mouse por " +
                 "encima de la bomba. Off = se pinta al sembrar y se queda, que es como se comportan " +
                 "todos los jefes ya autorados.")]
        public bool HoverOnlyPaint;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Gesto del jefe al sembrar. Va al AINode_SpawnRoomObjects de adentro, así que las " +
                 "bombas caen con su animación en vez de materializarse mientras él sigue en idle.")]
        public string SowFeedbackId;

        [NonSerialized] private AINode_SpawnRoomObjects _spawner;

        /// <summary>
        /// Siembras que ya pasaron, para saber por dónde va la rotación. Vive acá y no en el servicio
        /// porque es de este nodo: el árbol de runtime se arma de cero en cada pelea, así que cada
        /// combate vuelve a empezar por <see cref="BlastShape.Orthogonal"/>.
        /// </summary>
        [NonSerialized] private int _sowings;

        public override string NodeName =>
            $"Bomb Field ({Count}x {(Definition != null ? Definition.name : "?")}, {ShapeGlyph()})";

        private string ShapeGlyph() => Shape switch
        {
            BlastShape.Diagonal => "x",
            BlastShape.Alternating => "+/x",
            _ => "+",
        };

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
        /// Pasa por TODAS las que están en pie y no sólo por las nuevas. Lo que se pinta es lo que
        /// <c>Sow</c> devuelve y no la cruz recién calculada: a una bomba ya armada le contesta la
        /// suya, que es la que le va a estallar. Pintando la calculada, con las formas rotando, una
        /// bomba vieja quedaría avisando el aspa de la generación nueva.
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

            var shape = ShapeForSowing(_sowings);
            _sowings++;

            foreach (var (guid, coord) in spawner.LiveObjects())
            {
                var armed = field.Sow(guid, ComputeCross(coord, grid, shape), FuseTurns);
                if (armed == null) continue;

                var channel = ChannelFor(context.SelfGuid, ChannelPrefix, guid);
                // El Mark es incondicional: el flag decide quién DIBUJA la cruz, no si la bomba
                // amenaza. Sin marca no habría qué mostrar en el hover ni qué detonar.
                threat?.Mark(channel, armed, IgnitionDamage, AttackKind.Environmental);
                if (!HoverOnlyPaint) overlay?.Show(channel, armed);

                AttachBombTooltip(context, guid);
            }
        }

        /// <summary>Describe la siembra: cuántas bombas y con qué mecha.</summary>
        /// <remarks>
        /// <b>Sin casillas a propósito.</b> Las ranuras las sortea <c>AINode_SpawnRoomObjects</c>
        /// con <c>ScatteredFree</c> en el momento de sembrar, así que dónde van a caer no se sabe
        /// hasta que caen. Un conjunto vacío significa "no se sabe", nunca "estimado".
        /// </remarks>
        public bool TryDescribeIntent(AIContext context, out AIIntent intent)
        {
            intent = default;
            if (Definition == null) return false;

            intent = new AIIntent(
                "intent.bomb_field", "Siembra bombas",
                damage: 0, kind: AttackKind.Environmental,
                amount: Count, turnsAway: 0);
            return true;
        }

        /// <summary>
        /// Le cuelga a la bomba su propio hover: su tarjeta, su mecha y su cruz, no las del jefe.
        /// </summary>
        /// <remarks>
        /// <c>MarkNewBombs</c> recorre TODAS las bombas en pie en cada siembra, no sólo las nuevas,
        /// así que sale temprano si esta ya tiene su tooltip: re-suscribir el hover dejaría la cruz
        /// pintándose dos veces por bomba y por siembra.
        /// </remarks>
        private void AttachBombTooltip(AIContext context, Guid bombGuid)
        {
            if (context.VisualService == null) return;
            if (!context.VisualService.TryGetPawn(bombGuid, out var pawn) || pawn == null) return;
            if (pawn.gameObject.GetComponent<Visuals.RoomObjectTooltipInfo>() != null) return;

            var info = pawn.gameObject.AddComponent<Visuals.RoomObjectTooltipInfo>();
            info.Bind(Definition, context.SelfGuid, bombGuid);

            var trigger = Rollgeon.Entities.Visuals.EntityVisualService.AttachHoverTooltip(
                pawn, info.BuildTooltip);
            if (trigger == null) return;

            var bossGuid = context.SelfGuid;
            trigger.HoverChanged += on =>
            {
                var preview = EnemyIntentPreviewOverlay.ResolveOrCreate();
                if (on) preview.ShowForSubject(bossGuid, bombGuid);
                else preview.Clear();
            };
        }

        /// <summary>Qué forma le toca a la siembra número <paramref name="sowing"/>, contando de 0.</summary>
        public BlastShape ShapeForSowing(int sowing) => Shape switch
        {
            BlastShape.Alternating => sowing % 2 == 0 ? BlastShape.Orthogonal : BlastShape.Diagonal,
            var fixedShape => fixedShape,
        };

        /// <remarks>
        /// Los brazos se recortan contra la sala pero <b>no</b> se saltea el hueco: un brazo tapado
        /// por una pared no corre el estallido a la casilla siguiente, simplemente no existe. Es lo
        /// que hace que una bomba contra el borde avise menos casillas de las que avisaría en el
        /// medio, que es exactamente lo que después le estalla.
        /// </remarks>
        private static List<GridCoord> ComputeCross(GridCoord center, IGridManager grid, BlastShape shape)
        {
            var cross = new List<GridCoord>(5);
            AddUsable(cross, center, grid);

            if (shape == BlastShape.Diagonal)
            {
                AddUsable(cross, new GridCoord(center.X - 1, center.Y - 1), grid);
                AddUsable(cross, new GridCoord(center.X + 1, center.Y - 1), grid);
                AddUsable(cross, new GridCoord(center.X - 1, center.Y + 1), grid);
                AddUsable(cross, new GridCoord(center.X + 1, center.Y + 1), grid);
                return cross;
            }

            foreach (var n in center.Neighbors4()) AddUsable(cross, n, grid);
            return cross;
        }

        private static void AddUsable(List<GridCoord> cross, GridCoord coord, IGridManager grid)
        {
            if (grid.InBounds(coord) && grid.IsWalkable(coord)) cross.Add(coord);
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
