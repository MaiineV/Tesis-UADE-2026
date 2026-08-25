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
    /// Ese orden es correcto pero <b>no se lee</b> si las tres cosas salen en el mismo frame: el
    /// jugador ve fuego nuevo y bombas nuevas de golpe y no puede atribuir uno al otro. Por eso
    /// <see cref="TickCoroutine"/> mete el estallido en su propio beat bloqueante
    /// (<see cref="DetonationVfxId"/>) antes de sembrar. <see cref="Tick"/> —el camino síncrono,
    /// EditMode y escenas sin host de coroutines— hace las tres seguidas: bloquear ahí colgaría
    /// los tests.
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

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("VFX del estallido. Es el beat que separa el fuego de la siembra que sigue: vacío, " +
                 "las dos cosas aparecen en el mismo frame. Sin id no bloquea nada y degrada a " +
                 "silencio.")]
        public string DetonationVfxId;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feel (hitstop/shake) del estallido.")]
        public string DetonationFeelId;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Gesto del jefe al sembrar. Va al AINode_SpawnRoomObjects de adentro, así que las " +
                 "bombas caen con su animación en vez de materializarse mientras él sigue en idle.")]
        public string SowFeedbackId;

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

        /// <summary>
        /// El mismo orden que <see cref="Tick"/>, con el estallido cobrando su propio beat: prende el
        /// fuego, bloquea el turno mientras se ve, y recién ahí siembra.
        /// </summary>
        /// <remarks>
        /// La siembra va por el <c>TickCoroutine</c> del spawner y no por su <c>Tick</c>: es el que
        /// toca el gesto de <see cref="SowFeedbackId"/>, y sin eso las bombas nuevas volverían a
        /// aparecer solas.
        /// </remarks>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (context == null || Definition == null || FireTile == null ||
                context.Grid == null || context.Attributes == null)
            {
                onResult?.Invoke(Tick(context));
                yield break;
            }

            _crossByGuid ??= new Dictionary<Guid, List<GridCoord>>();

            if (DetonateSurvivors(context, context.Grid))
            {
                var blast = PlayDetonation(context);
                while (blast.MoveNext()) yield return blast.Current;
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

        /// <returns><c>true</c> si al menos una bomba llegó al plazo — lo único que amerita beat.</returns>
        private bool DetonateSurvivors(AIContext context, IGridManager grid)
        {
            if (_crossByGuid.Count == 0) return false;

            ServiceLocator.TryGetService<ISpecialTileService>(out var special);
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat);
            ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay);

            bool anyBlew = false;
            foreach (var kvp in _crossByGuid)
            {
                var guid = kvp.Key;
                var cross = kvp.Value;

                var health = context.Attributes.GetAttribute<Health>(guid);
                if (health != null && health.Value > 0)
                {
                    Detonate(context, grid, special, guid, cross);
                    anyBlew = true;
                }

                var channel = ChannelFor(context.SelfGuid, guid);
                threat?.Clear(channel);
                overlay?.Clear(channel);
            }

            _crossByGuid.Clear();
            return anyBlew;
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

        /// <remarks>
        /// Request armado a mano y no un <c>EffPlaySequence</c>: el nodo no nace de un effect pass,
        /// así que no tiene <c>EffectContext</c> que pasarle — mismo caso que el resto de los nodos
        /// de jefe. Sin <c>TurnManager</c> el gate no existe: la anim corre igual pero el turno no se
        /// retiene, y el estallido vuelve a pegarse a la siembra.
        /// </remarks>
        private IEnumerator PlayDetonation(AIContext context)
        {
            if (string.IsNullOrEmpty(DetonationVfxId) && string.IsNullOrEmpty(DetonationFeelId))
                yield break;
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var steps = new List<FeedbackSequenceStep>(2);
            if (!string.IsNullOrEmpty(DetonationVfxId)) steps.Add(Blast(DetonationVfxId));
            if (!string.IsNullOrEmpty(DetonationFeelId)) steps.Add(Blast(DetonationFeelId));

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
