using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Acción de "ataque telegráfico — turno N": marca un área centrada en el jugador, la resalta
    /// con estilo de advertencia y guarda el estado en <see cref="IThreatenedAreaService"/> para
    /// ejecutarla el próximo turno del Boss (<see cref="AINode_ExecuteTelegraph"/>). <b>No inflige
    /// daño este turno.</b> Sistemas prerequisito Bosses §1.
    /// </summary>
    /// <remarks>
    /// Dos avisos del mismo jefe piden dos <see cref="ChannelId"/>:
    /// <see cref="IThreatenedAreaService"/> guarda <i>un</i> área por fuente y el overlay pinta
    /// <i>un</i> área por fuente, así que dos marcas bajo el mismo guid no se suman — la segunda
    /// borra a la primera en los dos lados. El canal es lo que las separa (ver
    /// <see cref="SourceKey"/>).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TelegraphMark : AIActionNode
    {
        [Tooltip("Forma del área. Square=Boss1 (3×3), Row/Column=Boss2 (franja), HalfRoom=Boss3 (media sala), " +
                 "SquareAroundSelf=Boss1 (área centrada en el propio boss), CrossAroundSelf=cruz de brazos " +
                 "ortogonales de largo Size centrada en el boss (sin su celda).")]
        public ThreatShape Shape = ThreatShape.SquareAroundPlayer;

        [Tooltip("Radio para Square/SquareAroundSelf (1 ⇒ 3×3), ancho en casillas de la franja para Row/Column " +
                 "(1 ⇒ línea del jugador), medio-ancho de la banda perpendicular para DirectionalBand " +
                 "(1 ⇒ 3 casillas de ancho), semi-ancho del APEX para DirectionalCone (0 ⇒ arranca en " +
                 "1 casilla y se abre 1 por lado por paso), ancho de cada cuadrado para ScatteredSquares " +
                 "(2 ⇒ 2×2), o el " +
                 "índice de celda (1-based) para GridPartition. Ignorado en HalfRoom.")]
        [MinValue(0)]
        public int Size = 1;

        [Tooltip("Profundidad (en casillas) de la banda o el cono, arrancando pegada al boss. Solo para DirectionalBand y DirectionalCone.")]
        [MinValue(1)]
        [ShowIf("@Shape == ThreatShape.DirectionalBand || Shape == ThreatShape.DirectionalCone")]
        public int Depth = 2;

        [Tooltip("Cantidad de cuadrados independientes, anclados al azar en la sala. Solo para ScatteredSquares.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.ScatteredSquares)]
        public int Count = 3;

        [Tooltip("Columnas de la partición. Solo para GridPartition.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.GridPartition)]
        public int Columns = 3;

        [Tooltip("Filas de la partición. Solo para GridPartition.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.GridPartition)]
        public int Rows = 2;

        [Tooltip("Eje de corte para HalfRoom: Vertical ⇒ izquierda/derecha, Horizontal ⇒ abajo/arriba.")]
        [ShowIf(nameof(Shape), ThreatShape.HalfRoom)]
        public HalfRoomAxis HalfAxis = HalfRoomAxis.Vertical;

        [Tooltip("Daño que aplicará el ataque el próximo turno si el jugador sigue en el área.")]
        [MinValue(0)]
        public int Damage = 100;

        [Tooltip("Tipo de ataque del DamageContext al ejecutar.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Tooltip("Canal de la marca. Vacío = la fuente es el guid del propio jefe. " +
                 "Con nombre, la marca vive aparte de la principal del mismo jefe, y el paso que la " +
                 "consume (AINode_IgniteArea) tiene que declarar el MISMO canal.")]
        public string ChannelId;

        [Tooltip("Si true, no recorta el área a lo que el jefe ve — para ataques que arquean por " +
                 "encima de obstáculos (ej. artillería/mortero). Default false: mismo comportamiento " +
                 "de siempre (las shapes de IsLineOfSightGated se recortan a lo visible, y sin nada " +
                 "visible el paso falla — ver AINode_TelegraphMark.Tick).")]
        public bool IgnoreLineOfSight;

        [Title("Windup")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feedback que corre AL MARCAR (turno N), después de guardar el área — para un " +
                 "\"gesto de carga\" visible en el turno del telegraph. Vacío = se marca sin animación, " +
                 "mismo comportamiento que antes de este campo.")]
        public string WindupFeedbackId;

        public override string NodeName => string.IsNullOrEmpty(ChannelId)
            ? $"Telegraph Mark ({Shape}, dmg {Damage})"
            : $"Telegraph Mark [{ChannelId}] ({Shape}, dmg {Damage})";

        /// <summary>
        /// La fuente bajo la que vive una marca: el guid del jefe si no hay canal, uno derivado si
        /// lo hay. Público porque el paso que consume la marca tiene que resolverla igual.
        /// </summary>
        /// <remarks>
        /// Un canal es un guid derivado y no una key aparte, así que el área pendiente y el overlay
        /// quedan separados sin tocar el servicio, que sigue guardando una marca por fuente.
        /// <see cref="Guid.Empty"/> se conserva tal cual: es lo que hace que
        /// <see cref="IThreatenedAreaService.Mark"/> siga siendo no-op sin dueño, y un canal derivado
        /// de un guid vacío guardaría un área que nadie puede consumir.
        /// </remarks>
        public static Guid SourceKey(Guid selfGuid, string channelId)
            => selfGuid == Guid.Empty || string.IsNullOrEmpty(channelId)
                ? selfGuid
                : AINode_AuxTelegraph.ChannelGuid(selfGuid, channelId);

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;

            HashSet<GridCoord> tiles;
            // NeedsSelfAndPlayer en vez de comparar contra DirectionalBand: sin esto una forma
            // direccional nueva cae al else final, que la centra en el jugador y la manda a
            // Compute —que no la conoce— y sale vacia. El jefe pierde el turno con un warning.
            if (ThreatAreaShape.NeedsSelfAndPlayer(Shape))
            {
                if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;
                if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
                tiles = Shape == ThreatShape.DirectionalCone
                    ? ThreatAreaShape.ComputeDirectionalCone(grid, selfCoord, playerCoord, Size, Depth)
                    : ThreatAreaShape.ComputeDirectionalBand(grid, selfCoord, playerCoord, Size, Depth);
            }
            else if (Shape == ThreatShape.ScatteredSquares)
            {
                var rng = context.Rng ?? new System.Random();
                tiles = ThreatAreaShape.ComputeScatteredSquares(grid, rng, Count, Size);
            }
            // GridPartition necesita 3 parámetros (columnas, filas, índice de celda) y no entra en
            // el `size` único de Compute. Ni el jugador ni el boss son el centro, así que no hace
            // falta resolver ninguna posición.
            else if (Shape == ThreatShape.GridPartition)
            {
                tiles = ThreatAreaShape.ComputeGridPartition(grid, Columns, Rows, Size);
            }
            // AnchorsOnSelf en vez de comparar contra una shape puntual: el criterio es de la forma
            // y no del nodo, así que una shape nueva anclada en el jefe no pide otra rama acá.
            else if (ThreatAreaShape.AnchorsOnSelf(Shape))
            {
                if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
                tiles = ThreatAreaShape.Compute(grid, selfCoord, Shape, Size, HalfAxis);
            }
            else
            {
                if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;
                tiles = ThreatAreaShape.Compute(grid, playerCoord, Shape, Size, HalfAxis);
            }
            if (tiles.Count == 0)
            {
                Debug.LogWarning($"[AINode_TelegraphMark] Área vacía (shape={Shape}) — ¿grafo sin bounds? No se marca nada.");
                return AIResult.Failed;
            }

            // LOS de proyecto, SOLO al marcar (lo marcado detona como siempre): las formas
            // dirigidas se recortan a lo que el atacante realmente ve — la sombra detrás de una
            // mesa/bomba no se marca ni cobra. Las de sala pasan de largo (IsLineOfSightGated).
            // IgnoreLineOfSight se salta el recorte entero: pensado para ataques que arquean por
            // encima de obstáculos (artillería) y no deberían fallar el turno entero por un muro
            // en el medio.
            if (!IgnoreLineOfSight && ThreatAreaShape.IsLineOfSightGated(Shape)
                && grid.TryGetPosition(context.SelfGuid, out var losOrigin))
            {
                GridLineOfSight.FilterVisible(grid, losOrigin, tiles,
                                              context.SelfGuid, context.PlayerGuid);

                // Área toda en sombra: el ataque no tiene nada que marcar. Failed y no
                // Succeeded, así el Sequence/If de arriba puede caer a su plan B.
                if (tiles.Count == 0) return AIResult.Failed;
            }

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_TelegraphMark] IThreatenedAreaService no registrado. " +
                               "Agrega ThreatenedAreaServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            // La misma fuente para el área y para el overlay: Show limpia por fuente antes de
            // pintar, así que un canal que no coincidiera dejaría el aviso pendiente sin dibujo (o
            // el dibujo de otro aviso apagado a medias).
            var source = SourceKey(context.SelfGuid, ChannelId);

            // Marca y no pinta: el área se dibuja sólo al pasar el mouse por el enemigo
            // (EnemyIntentPreviewOverlay). El turno del jefe dura segundos y ahí nadie lee; el
            // jugador consulta el paño en el suyo, con tiempo para decidir dónde pararse —
            // regla Mewgenics del spec de tooltips.
            threat.Mark(source, tiles, Damage, Kind);

            FaceMarkedArea(context, tiles);

            return AIResult.Succeeded;
        }

        /// <summary>
        /// Gira hacia el centro de lo recién marcado. Sin esto el jefe se queda con la
        /// orientación que traía del movimiento anterior durante todo el turno de aviso — como si
        /// no estuviera apuntando a nada, y recién gira de golpe un turno después, al ejecutar.
        /// </summary>
        private static void FaceMarkedArea(AIContext context, HashSet<GridCoord> tiles)
        {
            if (context?.Grid == null || context.SelfGuid == Guid.Empty) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return;
            if (!ServiceLocator.TryGetService<Entities.Visuals.IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;

            pawn.FaceCoord(selfCoord, LastThreatenedAreaCenter.ComputeCenter(tiles));
        }

        /// <summary>
        /// Camino de play mode: marca igual que <see cref="Tick"/> (síncrono, la marca en sí no
        /// tiene nada que esperar) y, si hay <see cref="WindupFeedbackId"/>, corre ese gesto
        /// después — así el jugador ve al enemigo "cargar" en el turno que telegrafía, no recién
        /// en el que cobra.
        /// </summary>
        public override System.Collections.IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);
            if (result != AIResult.Succeeded || string.IsNullOrEmpty(WindupFeedbackId)
                || !ServiceLocator.TryGetService<Rollgeon.Feedback.IFeedbackService>(out var feedback) || feedback == null)
            {
                onResult?.Invoke(result);
                yield break;
            }

            var step = new Rollgeon.Feedback.FeedbackSequenceStep
            {
                Source = Rollgeon.Feedback.StepSource.FeedbackRef,
                FeedbackRefId = WindupFeedbackId,
                StartMode = Rollgeon.Feedback.StepStartMode.Immediate,
                EndMode = Rollgeon.Feedback.StepEndMode.OnDuration,
                BlockSequence = true,
            };

            ServiceLocator.TryGetService<Rollgeon.Combat.Actions.TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new Rollgeon.Feedback.FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<Rollgeon.Feedback.FeedbackSequenceStep> { step },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            if (turn != null && turn.IsWaitingForFeedback)
            {
                var wait = Rollgeon.Combat.Actions.TurnManager.WaitForFeedbackCompletion(turn);
                while (wait.MoveNext()) yield return wait.Current;
            }

            onResult?.Invoke(result);
        }

#if UNITY_EDITOR
        private static IEnumerable<string> GetFeedbackIdsForDropdown()
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:FeedbackDBSO"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var db = UnityEditor.AssetDatabase.LoadAssetAtPath<Rollgeon.Feedback.FeedbackDBSO>(path);
                if (db == null) continue;
                foreach (var id in db.GetAllFeedbackIds()) yield return id;
            }
        }
#endif
    }
}
