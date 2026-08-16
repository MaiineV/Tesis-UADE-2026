using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combos;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// "Tirar la mano" de La Generala (piso 3): tira tantos dados como dados vivos le queden en la
    /// mesa, corre la tirada por el <see cref="ComboResolver"/> — <b>el mismo detector de combos que
    /// usa el jugador</b> — y publica el resultado en <see cref="IBossDiceHandService"/> para que las
    /// ramas del árbol elijan el telegraph según el combo que salió.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Romper un dado borra una categoría, sin código nuevo.</b> Los combos ya exigen un mínimo de
    /// dados en el array (<c>Combo_Generala</c> ≥ 5, <c>Combo_Poker</c> ≥ 4, <c>Combo_Escalera</c> ≥ 5):
    /// tirando solo los dados vivos, con 4 dados la Generala deja de existir y con 3 se cae el Póker.
    /// </para>
    /// <para>
    /// <b>La ronda extra de aviso.</b> Un combo listado en <see cref="SlowCombos"/> se publica
    /// <i>cantado pero no armado</i>: ese turno nadie marca (todas las ramas piden mano armada), y al
    /// turno siguiente este nodo la arma <b>sin re-tirar</b>. Resultado: dos rondas entre la tirada y
    /// el impacto en vez de una, que es exactamente el "+1 ronda de aviso" de la mano grande.
    /// </para>
    /// <para>
    /// <b>Reroll (Fase 2).</b> Con rerolls habilitados (<c>AINode_SetHandReroll</c>) re-tira los dados
    /// que no contribuyen al combo detectado y se queda con la mejor de las dos manos por
    /// <c>BaseComboSO.Priority</c> — la misma mecánica que tiene el jugador.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_RollHand : AIActionNode
    {
        /// <summary>De dónde sale la cantidad de dados a tirar.</summary>
        public enum HandSizeSource
        {
            /// <summary>Tantos dados como aliados vivos tenga el boss (la mesa = sus dados).</summary>
            AliveAllies,

            /// <summary>Siempre <see cref="MaxDice"/> — para arenas sin mesa o para debug.</summary>
            Fixed,
        }

        [Tooltip("Cuántos dados tira: AliveAllies = tantos como dados vivos le queden en la mesa " +
                 "(sus aliados SON sus dados — no metas otros enemigos en la arena), Fixed = siempre MaxDice.")]
        public HandSizeSource SizeSource = HandSizeSource.AliveAllies;

        [Tooltip("Tope de dados de la mano (la mesa completa).")]
        [MinValue(1)]
        public int MaxDice = 5;

        [Tooltip("Caras de cada dado. La mesa de la casa juega con d6, igual que el jugador.")]
        [MinValue(2)]
        public int DieFaces = 6;

        [Tooltip("Combos que se cantan una ronda antes de armarse (+1 ronda de aviso). " +
                 "Vacío = toda mano arma en el mismo turno en que se tira.")]
        public List<string> SlowCombos = new List<string> { Rollgeon.Combos.ComboId.Generala };

        /// <remarks>
        /// Vacío cae al id canónico de La Generala, no a "sin animación": Odin no corre los
        /// inicializadores de campo al deserializar, así que un default por asignación llegaría en
        /// null desde los <c>ED_Boss_*.asset</c> ya autorados y la tirada quedaría muda hasta que
        /// alguien re-corriera el builder.
        /// </remarks>
        [Tooltip("Feedback de la tirada. Vacío = la animación de tirar dados de La Generala.")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        public string RollFeedbackId;

        public override string NodeName => $"Roll Hand ({MaxDice}d{DieFaces})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            var hands = BossDiceHandService.ResolveOrCreate();

            // Mano cantada el turno pasado: se arma sin re-tirar. Los dados siguen a la vista, así
            // que el jugador ve el mismo combo dos rondas antes de que caiga.
            if (hands.TryGetHand(context.SelfGuid, out var pending) && !pending.Armed)
            {
                hands.ArmHand(context.SelfGuid);
                return AIResult.Succeeded;
            }

            int diceCount = ResolveDiceCount(context);
            if (diceCount <= 0)
            {
                // Mesa entera rota: no hay dados que tirar. Mano vacía = bust (la rama de bust del
                // Selector cobra el mínimo), no un turno en blanco.
                hands.SetHand(context.SelfGuid, Array.Empty<int>(), BossDiceHand.NoCombo, armed: true);
                return AIResult.Succeeded;
            }

            var values = new int[diceCount];
            for (int i = 0; i < values.Length; i++) values[i] = NextFace(context);

            var detected = Detect(values, out int priority);
            values = ApplyRerolls(context, hands, values, ref detected, ref priority);

            string comboId = detected.IsMatch ? detected.ComboId : BossDiceHand.NoCombo;
            hands.SetHand(context.SelfGuid, values, comboId, armed: !IsSlow(comboId));
            return AIResult.Succeeded;
        }

        /// <summary>
        /// La tirada, con el cubilete a la vista. La mano se resuelve primero y la animación va
        /// después: los dados que caen ya son los definitivos, así que el jugador ve el mismo
        /// resultado que va a leer en la mesa.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Tirar los dados es <b>la</b> acción de La Generala y hasta acá no se veía: el jugador veía
        /// aparecer un combo en la UI sin que ella hiciera nada. El rig de dados existe justamente
        /// por este beat — <c>Roll</c> es la única animación del proyecto que es literalmente esto.
        /// </para>
        /// <para>
        /// El request se arma a mano porque el nodo no nace de un effect pass y no tiene
        /// <c>EffectContext</c> — el mismo caso que documenta <c>FeedbackRequest.Context</c> como
        /// nullable, y la misma forma que usan los nodos de jefe ya cableados.
        /// </para>
        /// </remarks>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);
            if (result != AIResult.Succeeded)
            {
                onResult?.Invoke(result);
                yield break;
            }

            var beat = PlayRoll(context);
            while (beat.MoveNext()) yield return beat.Current;

            onResult?.Invoke(result);
        }

        private IEnumerator PlayRoll(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = string.IsNullOrEmpty(RollFeedbackId)
                    ? BossFeedbackIds.GeneralaRollAnim
                    : RollFeedbackId,
                StartMode = StepStartMode.Immediate,
                EndMode = StepEndMode.OnDuration,
                BlockSequence = true,
            };

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = new List<FeedbackSequenceStep> { step },
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar: la anim corre igual pero el turno no se
            // retiene. Mismo degradado que el resto de los nodos de jefe.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        // ======================================================================
        // Tirada
        // ======================================================================

        private int NextFace(AIContext context)
        {
            int faces = DieFaces < 2 ? 2 : DieFaces;
            return context.Rng != null
                ? context.Rng.Next(1, faces + 1)
                : UnityEngine.Random.Range(1, faces + 1);
        }

        /// <summary>
        /// Corre la tirada por el catálogo de combos del jugador. <paramref name="priority"/> queda
        /// en <see cref="int.MinValue"/> cuando no hay match, así cualquier combo le gana al bust.
        /// </summary>
        private static ComboDetectionResult Detect(IReadOnlyList<int> values, out int priority)
        {
            priority = int.MinValue;
            if (!ServiceLocator.TryGetService<ComboCatalogSO>(out var catalog) || catalog == null)
            {
                Debug.LogWarning("[AINode_RollHand] ComboCatalogSO no registrado — la mano no puede " +
                                 "resolver combos y sale bust.");
                return ComboDetectionResult.NoMatch();
            }

            var result = ComboResolver.DetectBest(catalog, values, out var best);
            if (!result.IsMatch || best == null) return ComboDetectionResult.NoMatch();

            priority = best.Priority;
            return result;
        }

        /// <summary>
        /// Re-tira los dados que no forman el combo detectado, tantas veces como rerolls tenga
        /// habilitados, y se queda con la mejor mano por prioridad de combo.
        /// </summary>
        private int[] ApplyRerolls(AIContext context, IBossDiceHandService hands, int[] values,
            ref ComboDetectionResult detected, ref int priority)
        {
            int rerolls = hands.GetRerollsPerRound(context.SelfGuid);
            if (rerolls <= 0) return values;

            for (int pass = 0; pass < rerolls; pass++)
            {
                var keep = new HashSet<int>();
                if (detected.ContributingIndices != null)
                    foreach (var idx in detected.ContributingIndices) keep.Add(idx);

                // Mano que usa todos los dados (Generala, Escalera): no hay nada que re-tirar.
                if (keep.Count >= values.Length) break;

                var candidate = (int[])values.Clone();
                for (int i = 0; i < candidate.Length; i++)
                    if (!keep.Contains(i)) candidate[i] = NextFace(context);

                var candidateResult = Detect(candidate, out int candidatePriority);
                if (candidatePriority <= priority) continue;

                values = candidate;
                detected = candidateResult;
                priority = candidatePriority;
            }

            return values;
        }

        private bool IsSlow(string comboId)
        {
            if (string.IsNullOrEmpty(comboId) || SlowCombos == null) return false;
            foreach (var slow in SlowCombos)
                if (string.Equals(slow, comboId, StringComparison.Ordinal)) return true;
            return false;
        }

        // ======================================================================
        // Tamaño de la mano
        // ======================================================================

        /// <summary>
        /// Dados vivos en la mesa. Los dados son los aliados del boss, así que se cuentan igual que
        /// <c>PcAllyAliveExists</c> (HP &gt; 0 en el <see cref="AttributesManager"/>). Sin roster
        /// consultable cae permisivo a la mano completa: un servicio faltante no debe convertir al
        /// boss en un maniquí.
        /// </summary>
        private int ResolveDiceCount(AIContext context)
        {
            int max = MaxDice < 1 ? 1 : MaxDice;
            if (SizeSource == HandSizeSource.Fixed) return max;

            var attrs = context.Attributes;
            if (attrs == null) ServiceLocator.TryGetService<AttributesManager>(out attrs);
            if (attrs == null) return max;

            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null) return max;

            var allies = query.GetAllAlliesOf(context.SelfGuid);
            if (allies == null) return 0;

            int alive = 0;
            foreach (var ally in allies)
            {
                if (ally == null || ally.Guid == context.SelfGuid) continue;
                var hp = attrs.GetAttribute<Health>(ally.Guid);
                if (hp != null && hp.ModifiedValue > 0) alive++;
            }

            return alive < max ? alive : max;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Los ids de feedback nunca se tipean a mano (§0): el dropdown los lee del propio
        /// <see cref="FeedbackDBSO"/>, así un id renombrado se ve vacío en vez de fallar en silencio.
        /// </summary>
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
