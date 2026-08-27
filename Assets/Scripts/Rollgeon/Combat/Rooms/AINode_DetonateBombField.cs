using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Actions;
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
    /// Descuenta la mecha de las bombas de <see cref="IBombFieldService"/> y prende la cruz de las
    /// que llegaron a cero. La otra mitad de <see cref="AINode_BombField"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Va fuera del ciclo, tickeado todos los turnos.</b> Es lo que hace que la mecha se pueda
    /// medir en turnos y no en ciclos: dentro del <c>Alternate</c> sólo correría una vez cada tres
    /// turnos y el plazo volvería a ser un ciclo entero, que es la única duración que un nodo
    /// tickeado por ciclo puede expresar.
    /// </para>
    /// <para>
    /// Y va <b>antes</b> del ciclo: si corriera detrás, en el turno en que se siembra detonaría la
    /// generación que el mismo turno acaba de plantar.
    /// </para>
    /// <para>
    /// Romper una bomba a mano <b>no</b> deja fuego. El servicio la reporta aparte y este nodo sólo
    /// le levanta la cruz: el fuego es exclusivamente el premio por haberla dejado madurar.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_DetonateBombField : AIActionNode, IAIIntentNode
    {
        [Tooltip("Casilla especial que deja el estallido. La misma que autora el nodo que siembra.")]
        public SpecialTileDefinitionSO FireTile;

        [MinValue(0)]
        [Tooltip("Rondas que arde el fuego del estallido. 0 = usa el default del SO de FireTile.")]
        public int FireDurationRounds;

        [MinValue(0)]
        [Tooltip("Daño del estallido a quien siga parado en la cruz cuando prende.")]
        public int IgnitionDamage;

        [Tooltip("Tiene que ser el MISMO que el del nodo que siembra: de acá sale el canal de amenaza " +
                 "de cada bomba, y con otro prefijo levantaría cruces que nadie pintó.")]
        public string ChannelPrefix = "bomb.";

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("VFX del estallido. Es el beat que lo separa de lo que venga después en el turno: " +
                 "vacío, el fuego aparece sin que nada lo anuncie.")]
        public string DetonationVfxId;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feel (hitstop/shake) del estallido.")]
        public string DetonationFeelId;

        public override string NodeName => "Detonate Bomb Field";

        /// <summary>
        /// Este nodo no describe UNA cosa: describe una cruz por bomba en pie.
        /// </summary>
        public bool TryDescribeIntent(AIContext context, out AIIntent intent)
        {
            intent = default;
            return false;
        }

        /// <inheritdoc />
        public void DescribeIntents(AIContext context, List<AIIntent> into)
        {
            if (context == null || into == null || FireTile == null) return;

            var field = BombFieldService.ResolveOrCreate();
            int rounds = FireDurationRounds > 0 ? FireDurationRounds : FireTile.DefaultDurationRounds;

            foreach (var (guid, cross) in field.Live(context.Attributes))
            {
                if (!field.TryGetFuse(guid, out int fuse)) continue;

                // El daño es el del estallido en sí, que en el Croupier es CERO a propósito: todo
                // lo que cobra una bomba es el fuego que deja. Anunciar un golpe sería mentir.
                into.Add(new AIIntent(
                    AIIntentTextKeys.BombBlast, "Estalla",
                    IgnitionDamage, AttackKind.Environmental,
                    tiles: cross,
                    leaves: FireTile, leavesRounds: rounds,
                    turnsAway: fuse - 1,
                    channelKey: AINode_BombField.ChannelFor(context.SelfGuid, ChannelPrefix, guid),
                    subjectGuid: guid));
            }
        }

        public override AIResult Tick(AIContext context)
        {
            Resolve(context, null);
            return AIResult.Succeeded;
        }

        /// <summary>El estallido cobra su propio beat: sin esto el fuego sale en el mismo frame que
        /// lo que el jefe haga después en el turno.</summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var blasts = new List<GridCoord>();
            if (Resolve(context, blasts))
            {
                var beat = PlayDetonation(context, blasts);
                while (beat.MoveNext()) yield return beat.Current;
            }

            onResult?.Invoke(AIResult.Succeeded);
        }

        /// <param name="blasts">
        /// Recibe la casilla de cada bomba que llegó al plazo, para que el chispazo salga de donde
        /// reventó. <c>null</c> en el camino síncrono, que no presenta nada.
        /// </param>
        /// <returns><c>true</c> si al menos una bomba llegó al plazo — lo único que amerita beat.</returns>
        private bool Resolve(AIContext context, List<GridCoord> blasts)
        {
            if (context?.Grid == null || context.Attributes == null) return false;

            var field = BombFieldService.ResolveOrCreate();

            // Listas locales y no campos reusados: Odin NO corre field initializers al deserializar,
            // asi que un campo con `= new List<>()` llega en null en la copia de runtime que arma
            // EnemyDataSO.CreateRuntimeAIRoot. Es una alocacion por turno del jefe.
            var due = new List<(Guid Guid, IReadOnlyList<GridCoord> Cross)>();
            var broken = new List<(Guid Guid, IReadOnlyList<GridCoord> Cross)>();
            field.TickFuses(context.Attributes, due, broken);

            if (due.Count == 0 && broken.Count == 0) return false;

            ServiceLocator.TryGetService<ISpecialTileService>(out var special);
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat);
            ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay);

            foreach (var (guid, cross) in due)
            {
                // La casilla se lee ANTES de detonar: Detonate saca la bomba del grid, y después
                // ya no hay dónde anclar el chispazo.
                if (blasts != null && context.Grid.TryGetPosition(guid, out var at)) blasts.Add(at);

                Detonate(context, special, guid, cross);
                LiftCross(context.SelfGuid, guid, threat, overlay);
            }

            // Las rotas a mano ya no están en el paño; lo único que les queda es el aviso pintado.
            foreach (var (guid, _) in broken)
                LiftCross(context.SelfGuid, guid, threat, overlay);

            return due.Count > 0;
        }

        private void LiftCross(
            Guid selfGuid, Guid bombGuid, IThreatenedAreaService threat, IThreatOverlayService overlay)
        {
            var channel = AINode_BombField.ChannelFor(selfGuid, ChannelPrefix, bombGuid);
            threat?.Clear(channel);
            overlay?.Clear(channel);
        }

        private void Detonate(
            AIContext context, ISpecialTileService special, Guid guid, IReadOnlyList<GridCoord> cross)
        {
            if (FireTile == null)
            {
                Debug.LogWarning("[AINode_DetonateBombField] Falta FireTile — la bomba desaparece sin " +
                                 "dejar fuego.");
            }
            else
            {
                special?.Place(FireTile, cross, new TilePlacementOptions
                {
                    Owner = context.SelfGuid,
                    DurationRounds = FireDurationRounds > 0 ? FireDurationRounds : 0,
                });
            }

            ChargeIgnitionDamage(context, cross);

            context.VisualService?.Despawn(guid);
            context.Grid.Unregister(guid);
            RoomObjectCleanupService.ResolveOrCreate().Forget(guid);

            // El spawner que siembra recién nota la baja en su PROPIO Tick (CollectBroken mira
            // Health), y esta detonación pasa por afuera de esa vía — sin esto la ranura le queda
            // viva a sus ojos y nunca se resiembra.
            context.Attributes.SetAttributeValue<Health, int>(guid, 0);
        }

        private void ChargeIgnitionDamage(AIContext context, IReadOnlyList<GridCoord> cross)
        {
            if (IgnitionDamage <= 0 || context.DamagePipeline == null) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return;
            if (!ContainsCoord(cross, playerCoord)) return;

            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = IgnitionDamage,
                Kind = AttackKind.Environmental,
            });
        }

        /// <remarks>
        /// <para>
        /// Request armado a mano y no un <c>EffPlaySequence</c>: el nodo no nace de un effect pass,
        /// así que no tiene <c>EffectContext</c> que pasarle — mismo caso que el resto de los nodos
        /// de jefe. Sin <c>TurnManager</c> el gate no existe: la anim corre igual pero el turno no se
        /// retiene.
        /// </para>
        /// <para>
        /// El chispazo va anclado en la casilla de la bomba (<c>WorldPosition</c>) y <b>no</b> en el
        /// jugador. Sobre el jugador, una salva de tres bombas en rincones distintos daba un solo
        /// destello encima suyo — y en el turno de reparto, donde también sale el disparo, ese
        /// destello se leía como un segundo impacto del tiro.
        /// </para>
        /// <para>
        /// Los estallidos que no abren el beat se piden sueltos, sin tocar el gate del turno: son
        /// simultáneos, y retenerlos de a uno convertiría la salva en una fila de destellos.
        /// </para>
        /// </remarks>
        private IEnumerator PlayDetonation(AIContext context, List<GridCoord> blasts)
        {
            if (string.IsNullOrEmpty(DetonationVfxId) && string.IsNullOrEmpty(DetonationFeelId))
                yield break;
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            if (!string.IsNullOrEmpty(DetonationVfxId) && blasts != null)
            {
                for (int i = 1; i < blasts.Count; i++)
                {
                    feedback.RequestFeedbackBlocking(BlastRequest(
                        context, new List<FeedbackSequenceStep> { Blast(DetonationVfxId) },
                        context.Grid.GridToWorld(blasts[i])), null);
                }
            }

            var steps = new List<FeedbackSequenceStep>(2);
            if (!string.IsNullOrEmpty(DetonationVfxId)) steps.Add(Blast(DetonationVfxId));
            if (!string.IsNullOrEmpty(DetonationFeelId)) steps.Add(Blast(DetonationFeelId));

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(
                BlastRequest(context, steps, FirstBlastWorld(context, blasts)),
                () => turn?.OnFeedbackComplete());

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        /// <remarks>
        /// <c>TargetGuid</c> es el propio jefe y no el jugador: el estallido es cosa suya, y una entry
        /// autorada <c>AtTarget</c> tiene que caer sobre la mesa, no sobre quien la está sufriendo.
        /// </remarks>
        private static FeedbackRequest BlastRequest(
            AIContext context, List<FeedbackSequenceStep> steps, Vector3 world) => new FeedbackRequest
        {
            IsSequence = true,
            SequenceSteps = steps,
            SourceGuid = context.SelfGuid,
            TargetGuid = context.SelfGuid,
            WorldPosition = world,
        };

        /// <summary>
        /// Dónde sale el estallido que abre el beat. Sin casilla —camino síncrono, o una bomba que
        /// salió del grid antes de tiempo— cae en la del jefe, que es de donde salió la bomba.
        /// </summary>
        private static Vector3 FirstBlastWorld(AIContext context, List<GridCoord> blasts)
        {
            if (blasts != null && blasts.Count > 0) return context.Grid.GridToWorld(blasts[0]);
            if (context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord))
                return context.Grid.GridToWorld(selfCoord);
            return Vector3.zero;
        }

        /// <summary>IReadOnlyList no trae Contains, y Linq sobre la cruz aloca por estallido.</summary>
        private static bool ContainsCoord(IReadOnlyList<GridCoord> cross, GridCoord coord)
        {
            for (int i = 0; i < cross.Count; i++)
                if (cross[i].Equals(coord)) return true;
            return false;
        }

        private static FeedbackSequenceStep Blast(string feedbackId) => new FeedbackSequenceStep
        {
            Source = StepSource.FeedbackRef,
            FeedbackRefId = feedbackId,
            StartMode = StepStartMode.Immediate,
            EndMode = StepEndMode.OnDuration,
            BlockSequence = true,
        };

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
