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
    /// La escarcha de la mesa: La Generala congela el cuadrado de <see cref="Radius"/> a la redonda
    /// (<see cref="Solid"/>) o sólo su borde. Entrar cuesta el turno (<see cref="IStunService"/>, vía
    /// <see cref="IceStunBinder"/>); ya estar adentro cuando cae, no.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cero daño a propósito: el techo por turno del piso ya está lleno con la mano y el cubilete,
    /// así que la escarcha cobra en <b>turnos</b> — el que se congela pierde el suyo y come la mano
    /// siguiente sin poder esquivarla.
    /// </para>
    /// <para>
    /// <b>Ella no se congela.</b> El área se publica con ella como dueña y el binder ignora los
    /// triggers del dueño; con <see cref="Solid"/> eso es necesario, no cómodo, porque su propia
    /// casilla queda adentro y el reposicionamiento corre después en el mismo turno. El área queda
    /// donde la puso: la escarcha es del piso, no de ella.
    /// </para>
    /// <para>
    /// <b><see cref="Solid"/> en un asset viejo llega en false</b> — Odin no corre los
    /// inicializadores de campo al deserializar. Re-correr el builder lo arregla.
    /// </para>
    /// <para>
    /// Devuelve <c>Failed</c> cuando no hay anillo posible ⇒ va SIEMPRE dentro de un
    /// <c>Selector[nodo, Wait]</c>.
    /// </para>
    /// </remarks>
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

        /// <summary>Anillo vivo publicado por este nodo. Por pelea: el árbol se clona al spawn.</summary>
        [NonSerialized] private Guid _liveRingId;

        public override string NodeName =>
            $"Generala — Escarcha ({(Solid ? "área" : "anillo")} r{Radius})";

        /// <remarks>
        /// Vacío significa "el id canónico del nodo", no "sin animación": Odin puede deserializar un
        /// <c>ED_Boss_*.asset</c> viejo sin correr los field initializers, y un default en el campo
        /// llegaría en null. Mismo criterio que <see cref="AINode_GeneralaCupSlam"/>.
        /// </remarks>
        private string AnimFeedbackId => string.IsNullOrEmpty(AnimFeedbackIdOverride)
            ? BossFeedbackIds.GeneralaFrostAnim
            : AnimFeedbackIdOverride;

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): congela sin gesto, porque
        /// no hay dónde esperarlo.
        /// </summary>
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

        /// <summary>
        /// Camino de play mode: congela primero y recién después presenta. El estado del turno nunca
        /// queda esperando un VFX (mismo criterio que <see cref="AINode_RotateBlock"/>).
        /// </summary>
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

        /// <summary>
        /// Casillas a distancia Chebyshev <b>exactamente</b> <paramref name="radius"/> de
        /// <paramref name="center"/>, caminables y dentro de la sala: el borde hueco. Pura y estática.
        /// </summary>
        /// <remarks>
        /// No vive en <c>ThreatAreaShape</c> porque no es una <c>ThreatShape</c>: no se marca ni se
        /// telegrafía, se publica como área de hazard. El día que un segundo jefe quiera un anillo
        /// avisado, ahí sí conviene subirla.
        /// </remarks>
        public static List<GridCoord> ComputeRing(IGridManager grid, GridCoord center, int radius) =>
            Compute(grid, center, radius, solid: false);

        /// <summary>
        /// Casillas a distancia Chebyshev <b>hasta</b> <paramref name="radius"/> de
        /// <paramref name="center"/>, la del centro incluida: el cuadrado macizo.
        /// </summary>
        public static List<GridCoord> ComputeArea(IGridManager grid, GridCoord center, int radius) =>
            Compute(grid, center, radius, solid: true);

        /// <summary>
        /// La forma, en un solo lugar. <paramref name="solid"/> es lo único que cambia: <c>==</c>
        /// contra <c>&lt;=</c> en la comparación de distancia. Dos loops separados terminarían
        /// divergiendo en el filtro de sala, que es la mitad que importa.
        /// </summary>
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

        /// <remarks>
        /// Request armado a mano en vez de un <c>EffPlaySequence</c>: el nodo no nace de un effect
        /// pass, así que no tiene <c>EffectContext</c> que pasarle — mismo caso que
        /// <see cref="AINode_GeneralaCupSlam"/>.
        /// </remarks>
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
    }
}
