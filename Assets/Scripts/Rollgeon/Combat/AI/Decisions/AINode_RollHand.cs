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
    /// "Tirar la mano" de La Generala: tira tantos dados como vivos le queden en la mesa, los corre
    /// por el <see cref="ComboResolver"/> —el mismo detector que usa el jugador— y publica el
    /// resultado en <see cref="IBossDiceHandService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Romper un dado borra una categoría sin código nuevo: los combos exigen un mínimo de dados en
    /// el array, así que con 4 la Generala deja de existir y con 3 se cae el Póker.
    /// </para>
    /// <para>
    /// Un combo de <see cref="SlowCombos"/> se publica cantado pero no armado: ese turno nadie marca
    /// (todas las ramas piden mano armada) y al siguiente este nodo la arma <b>sin re-tirar</b>.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_RollHand : AIActionNode
    {
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
        /// Vacío cae al id canónico, no a "sin animación": Odin no corre los inicializadores de
        /// campo al deserializar, y un default por asignación llegaría null desde los
        /// <c>ED_Boss_*.asset</c> ya autorados.
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

            // Mano cantada el turno pasado: se arma sin re-tirar.
            if (hands.TryGetHand(context.SelfGuid, out var pending) && !pending.Armed)
            {
                hands.ArmHand(context.SelfGuid);
                return AIResult.Succeeded;
            }

            int diceCount = ResolveDiceCount(context);
            if (diceCount <= 0)
            {
                // Mesa entera rota. Mano vacía = bust (la rama de bust del Selector cobra el
                // mínimo), no un turno en blanco.
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

        /// <summary>La mano se resuelve <b>antes</b> de animar: los dados que caen son definitivos.</summary>
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

            // Sin TurnManager no hay gate: la anim corre igual pero el turno no se retiene.
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
        /// <paramref name="priority"/> queda en <see cref="int.MinValue"/> sin match, así cualquier
        /// combo le gana al bust.
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
        /// Re-tira lo que no forma el combo detectado y se queda con la mejor mano por prioridad.
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
        /// Los dados son los aliados del boss, contados como <c>PcAllyAliveExists</c>. Sin roster
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
        // Los ids de feedback nunca se tipean a mano (§0): leídos del FeedbackDBSO, un id renombrado
        // se ve vacío en vez de fallar en silencio.
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
