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
    /// La escarcha de la mesa: La Generala congela el anillo de casillas que está a
    /// <see cref="Radius"/> exactas de ella. Cruzarlo cuesta el turno
    /// (<see cref="IStunService"/>, vía <see cref="IceStunBinder"/>); quedarse adentro o afuera no
    /// cuesta nada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Anillo y no área maciza.</b> El hueco central —las casillas a distancia &lt;
    /// <see cref="Radius"/>— es justamente desde donde el jugador le rompe los dados, y llenarlo de
    /// hielo mataría la única jugada que le borra categorías a la mano. Lo que cobra la escarcha es
    /// <i>cruzar</i>: entrar a la mesa y salir de ella. La distancia sigue siendo la variable que
    /// elige el jugador, igual que con el cubilete.
    /// </para>
    /// <para>
    /// <b>Cero daño, a propósito.</b> El techo de daño por turno del piso 3 es 45 (65 con aviso), y
    /// su turno ya puede sumar 45 de la mano detonada + 18 del cubilete = 63. No queda presupuesto
    /// para un golpe más, así que la escarcha cobra en <b>turnos</b>: el que se congela pierde el
    /// suyo y come la mano de la ronda siguiente sin poder esquivarla — que es el golpe extra que
    /// pide el diseño, ya presupuestado. Mismo criterio que la estela del Anotador
    /// (<c>Damage = 0</c> en la definición del hazard).
    /// </para>
    /// <para>
    /// <b>Ella no se congela.</b> El anillo se publica con ella como dueña y el binder ignora los
    /// triggers del dueño: el nodo de reposicionamiento corre después en su turno y la haría cruzar
    /// su propio hielo. El anillo queda donde estaba la mesa cuando lo puso — la escarcha es del
    /// piso, no de ella.
    /// </para>
    /// <para>
    /// <b>Un anillo vivo por vez.</b> Con <see cref="ReplacePreviousRing"/> el anillo nuevo apaga el
    /// anterior antes de publicarse: dos anillos superpuestos duplicarían overlays y dejarían medio
    /// mapa helado, que es lo contrario de una regla que se lee de un vistazo.
    /// </para>
    /// <para>
    /// <b>Devuelve <c>Failed</c> cuando no hay anillo posible</b> (sala sin bounds, hazard sin
    /// asignar, servicio ausente) ⇒ va SIEMPRE dentro de un <c>Selector[nodo, Wait]</c>, como el
    /// resto de los nodos riesgosos de su árbol.
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

        [Tooltip("Distancia Chebyshev EXACTA a la que cae el anillo. 2 = el borde del 5×5 que la " +
                 "rodea; las casillas pegadas a ella quedan libres para poder romperle los dados.")]
        [MinValue(1)]
        public int Radius = 2;

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

        public override string NodeName => $"Generala — Escarcha (anillo r{Radius})";

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

            var ring = ComputeRing(grid, selfCoord, Radius);
            if (ring.Count == 0) return AIResult.Failed;

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

            var instanceId = hazards.Activate(Hazard, ring);
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
        /// <paramref name="center"/>, caminables y dentro de la sala. Pura y estática: es la forma
        /// del ataque y se testea sin montar servicios.
        /// </summary>
        /// <remarks>
        /// No vive en <c>ThreatAreaShape</c> porque no es una <c>ThreatShape</c>: no se marca ni se
        /// telegrafía, se publica como área de hazard. El día que un segundo jefe quiera un anillo
        /// avisado, ahí sí conviene subirla.
        /// </remarks>
        public static List<GridCoord> ComputeRing(IGridManager grid, GridCoord center, int radius)
        {
            var ring = new List<GridCoord>();
            if (grid == null) return ring;

            int r = radius < 1 ? 1 : radius;
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;

                var coord = new GridCoord(center.X + dx, center.Y + dy);
                if (grid.InBounds(coord) && grid.IsWalkable(coord)) ring.Add(coord);
            }

            return ring;
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
