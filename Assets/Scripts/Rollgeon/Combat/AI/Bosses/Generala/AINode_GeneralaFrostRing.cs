using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Status;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Generala
{
    /// <summary>
    /// El área se publica con ella como dueña y el binder ignora los triggers del dueño: con
    /// <see cref="Solid"/> su propia casilla queda adentro y sin eso se congelaría a sí misma.
    /// <see cref="Solid"/> llega en false en un asset viejo — Odin no corre los inicializadores de
    /// campo. Devuelve <c>Failed</c> sin anillo posible, así que va en un <c>Selector[nodo, Wait]</c>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_GeneralaFrostRing : AIActionNode
    {
        [Tooltip("Definición del hielo. Trigger = OnEnter, Damage = 0, ConsumeOnTrigger = true y " +
                 "DurationRounds = 2 (la escarcha nace en el turno del jefe, con el turno del " +
                 "jugador de esa ronda ya jugado: DurationRounds = D deja D-1 rondas pisables, así " +
                 "que 'dura 1 turno' se autora como 2). Ver HazardDefinitionSO.")]
        public HazardDefinitionSO Hazard;

        [Tooltip("Alcance Chebyshev de la escarcha. 2 = el 5×5 que la rodea, que es su mesa.")]
        [MinValue(1)]
        public int Radius = 2;

        [Tooltip("True = congela el cuadrado entero hasta Radius (área, su casilla incluida). " +
                 "False = sólo el borde exacto, dejando el centro libre.")]
        public bool Solid = true;

        [Tooltip("Turnos de stun al pisar el anillo. ApplyStun toma max(actual, nuevo): dos " +
                 "pisadas seguidas siguen siendo 1 turno.")]
        [MinValue(1)]
        public int StunTurns = 1;

        [Tooltip("Si true, el anillo nuevo reemplaza al anterior (un solo anillo vivo).")]
        public bool ReplacePreviousRing = true;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Override del gesto de la escarcha. Vacío = " + BossFeedbackIds.GeneralaFrostAnim + ".")]
        public string AnimFeedbackIdOverride;

        [NonSerialized] private Guid _liveRingId;

        public override string NodeName =>
            $"Generala — Escarcha ({(Solid ? "área" : "anillo")} r{Radius})";

        /// <summary>Vacío significa "el id canónico del nodo", no "sin animación": Odin no corre los field initializers al deserializar un <c>ED_Boss_*.asset</c>.</summary>
        private string AnimFeedbackId => string.IsNullOrEmpty(AnimFeedbackIdOverride)
            ? BossFeedbackIds.GeneralaFrostAnim
            : AnimFeedbackIdOverride;

        /// <summary>Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): congela sin gesto, porque no hay dónde esperarlo.</summary>
        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;
            if (Hazard == null)
            {
                Debug.LogError("[AINode_GeneralaFrostRing] Sin HazardDefinitionSO asignada — no hay escarcha.");
                return AIResult.Failed;
            }

            var grid = context.Grid;
            if (grid == null) ServiceLocator.TryGetService<IGridManager>(out grid);
            if (grid == null) return AIResult.Failed;

            if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;

            var frozen = Compute(grid, selfCoord, Radius, Solid);
            if (frozen.Count == 0) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<IHazardService>(out var hazards) || hazards == null)
            {
                Debug.LogError("[AINode_GeneralaFrostRing] IHazardService no registrado. " +
                               "Agregá HazardServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            var binder = IceStunBinder.ResolveOrCreate();
            if (binder == null) return AIResult.Failed;

            if (ReplacePreviousRing && _liveRingId != Guid.Empty)
            {
                hazards.Deactivate(_liveRingId);
                binder.ForgetIce(_liveRingId);
                _liveRingId = Guid.Empty;
            }

            var instanceId = hazards.Activate(Hazard, frozen);
            if (instanceId == Guid.Empty) return AIResult.Failed;

            // Trackear DESPUÉS de activar: el binder necesita el id para reconocer sus propios
            // triggers y saber a quién no stunear (la dueña del anillo).
            binder.TrackIce(instanceId, Hazard, context.SelfGuid, StunTurns);
            _liveRingId = instanceId;
            return AIResult.Succeeded;
        }

        /// <summary>Congela primero y recién después presenta, así el estado del turno nunca queda esperando un VFX.</summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);

            if (result == AIResult.Succeeded)
            {
                var show = PlayFrost(context);
                while (show.MoveNext()) yield return show.Current;
            }

            onResult?.Invoke(result);
        }

        /// <summary>Casillas a distancia Chebyshev <b>exactamente</b> <paramref name="radius"/> del centro, caminables y dentro de la sala: el borde hueco.</summary>
        public static List<GridCoord> ComputeRing(IGridManager grid, GridCoord center, int radius) =>
            Compute(grid, center, radius, solid: false);

        /// <summary>Casillas a distancia Chebyshev <b>hasta</b> <paramref name="radius"/> del centro, la del centro incluida: el cuadrado macizo.</summary>
        public static List<GridCoord> ComputeArea(IGridManager grid, GridCoord center, int radius) =>
            Compute(grid, center, radius, solid: true);

        /// <summary>La forma, en un solo lugar: <paramref name="solid"/> es lo único que cambia (<c>==</c> contra <c>&lt;=</c>).</summary>
        private static List<GridCoord> Compute(IGridManager grid, GridCoord center, int radius, bool solid)
        {
            var tiles = new List<GridCoord>();
            if (grid == null) return tiles;

            int r = radius < 1 ? 1 : radius;
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                int chebyshev = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                if (solid ? chebyshev > r : chebyshev != r) continue;

                var coord = new GridCoord(center.X + dx, center.Y + dy);
                if (grid.InBounds(coord) && grid.IsWalkable(coord)) tiles.Add(coord);
            }

            return tiles;
        }

        private IEnumerator PlayFrost(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            var steps = new List<FeedbackSequenceStep>
            {
                new FeedbackSequenceStep
                {
                    Source = StepSource.FeedbackRef,
                    FeedbackRefId = AnimFeedbackId,
                    StartMode = StepStartMode.Immediate,
                    EndMode = StepEndMode.OnDuration,
                    BlockSequence = true,
                },
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = steps,
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

#if UNITY_EDITOR
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
