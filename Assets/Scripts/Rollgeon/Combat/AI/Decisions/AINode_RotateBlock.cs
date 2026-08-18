using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Feedback;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Recalcula el "estado bloqueado rotativo" del Boss al cerrar su turno. Modo <b>Dice</b>:
    /// sortea <see cref="Count"/> dados de la build y los bloquea vía
    /// <see cref="IDiceBlockService"/>. Modo <b>Combo</b>: prohíbe los últimos <see cref="Count"/>
    /// combos del <see cref="IComboLogService"/> vía <see cref="IContractModifierService"/>, en
    /// ventana deslizante.
    /// </summary>
    /// <remarks>
    /// <b>Acá un id de feedback vacío significa silencio</b>, al revés que en los nodos propios de
    /// un jefe (donde vacío ⇒ el id canónico): el nodo lo comparten tres jefes y dos fueron
    /// autorados sin estos campos.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_RotateBlock : AIActionNode
    {
        public enum BlockTarget { Dice, Combo }

        [Tooltip("Dice = Boss 1 (sortea dados). Combo = Boss 2 (bloquea los últimos N combos del log).")]
        public BlockTarget Target = BlockTarget.Dice;

        [Tooltip("Cuántos dados sortear (Boss 1) o tamaño de la ventana de combos (Boss 2). Fase 1 = 1, Fase 2 = 2.")]
        [MinValue(1)]
        public int Count = 1;

        [OdinSerialize]
        [ShowIf(nameof(Target), BlockTarget.Dice)]
        [Tooltip("Opcional — sólo para Target = Dice. Si está seteado, el dado bloqueado no se sortea: " +
                 "el índice sale de este reader (ej. el número que canta el Croupier), y Count se " +
                 "ignora. Un índice mayor que la build da la vuelta (módulo); negativo = no bloquea " +
                 "nada. Vacío = comportamiento histórico (sorteo al azar de Count dados).")]
        public AIIntReader DirectedIndex;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("VFX sobre el jugador al que le sacan el dado. Vacío = sin presentación (ver remarks).")]
        public string BlockVfxId;

#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Feel (hitstop/shake) de la confiscación. Vacío = sin presentación.")]
        public string BlockFeelId;

        public override string NodeName => DirectedIndex != null && Target == BlockTarget.Dice
            ? "Rotate Block (Dice, directed)"
            : $"Rotate Block ({Target} ×{Count})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): el bloqueo sin la
        /// presentación.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            return Target == BlockTarget.Dice ? RotateDice(context) : RotateCombo(context);
        }

        /// <summary>
        /// Camino de play mode. El bloqueo se aplica antes de presentar: el estado del turno nunca
        /// queda esperando a un VFX.
        /// </summary>
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);

            // Sólo se presenta lo que se ve: un turno sin número cantado (índice -1) no bloqueó nada.
            if (result == AIResult.Succeeded && BlockedSomething(context))
            {
                var show = PlayConfiscation(context);
                while (show.MoveNext()) yield return show.Current;
            }

            onResult?.Invoke(result);
        }

        /// <summary>
        /// <c>true</c> si quedó algún dado bloqueado tras el tick. Se consulta el servicio porque el
        /// modo Combo no bloquea dados y el sorteo puede quedar en cero con una bolsa vacía.
        /// </summary>
        private bool BlockedSomething(AIContext context)
        {
            if (Target != BlockTarget.Dice) return false;
            if (!ServiceLocator.TryGetService<IDiceBlockService>(out var dice) || dice == null) return false;
            return dice.BlockedIndices != null && dice.BlockedIndices.Count > 0;
        }

        /// <remarks>
        /// Request armado a mano en vez de un <c>EffPlaySequence</c>: el nodo no nace de un effect
        /// pass, así que no tiene <c>EffectContext</c> que pasarle.
        /// </remarks>
        private IEnumerator PlayConfiscation(AIContext context)
        {
            var steps = new List<FeedbackSequenceStep>(2);
            if (!string.IsNullOrEmpty(BlockVfxId)) steps.Add(Step(BlockVfxId));
            if (!string.IsNullOrEmpty(BlockFeelId)) steps.Add(Step(BlockFeelId));
            if (steps.Count == 0) yield break;

            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null)
                yield break;

            ServiceLocator.TryGetService<TurnManager>(out var turn);
            turn?.BeginFeedbackWait();
            feedback.RequestFeedbackBlocking(new FeedbackRequest
            {
                IsSequence = true,
                SequenceSteps = steps,
                SourceGuid = context.SelfGuid,
                TargetGuid = context.PlayerGuid,
            }, () => turn?.OnFeedbackComplete());

            // Sin TurnManager no hay gate que esperar: la presentación corre igual, pero el turno
            // del jefe le pasa por encima.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        /// <summary>VFX y Feel arrancan juntos: son las dos mitades del mismo instante.</summary>
        private static FeedbackSequenceStep Step(string feedbackId) => new FeedbackSequenceStep
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

        // -- Boss 1: dados aleatorios -------------------------------------------------
        private AIResult RotateDice(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IDiceBlockService>(out var dice) || dice == null)
            {
                Debug.LogError("[AINode_RotateBlock] IDiceBlockService no registrado. " +
                               "Agrega DiceBlockServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            int bagSize = ResolveBagSize(context);
            if (bagSize <= 0) return AIResult.Failed;

            // Fresh cada turno: limpiamos y sorteamos Count dados distintos.
            dice.Clear();

            if (DirectedIndex != null) return BlockDirected(context, dice, bagSize);

            int toBlock = Count < bagSize ? Count : bagSize;
            var indices = new List<int>(bagSize);
            for (int i = 0; i < bagSize; i++) indices.Add(i);

            // Fisher-Yates parcial con el RNG del contexto (determinista en tests).
            for (int i = 0; i < toBlock; i++)
            {
                int j = i + NextInt(context, bagSize - i);
                (indices[i], indices[j]) = (indices[j], indices[i]);
                dice.Block(indices[i]);
            }

            return AIResult.Succeeded;
        }

        /// <summary>
        /// Bloqueo dirigido: un solo dado, con el índice de <see cref="DirectedIndex"/> en vez del
        /// sorteo. <see cref="Count"/> no aplica.
        /// </summary>
        /// <remarks>
        /// Módulo y no clamp: los números de la mecánica pueden exceder la build (6 sectores contra
        /// una bolsa de 5 dados) y clampear le daría al último dado el doble de probabilidad. Índice
        /// negativo = no confisques nada.
        /// </remarks>
        private AIResult BlockDirected(AIContext context, IDiceBlockService dice, int bagSize)
        {
            int raw = DirectedIndex.Read(context);
            if (raw < 0) return AIResult.Succeeded;

            // La etiqueta es el número de la mecánica, no el slot: el reader devuelve base-0 para
            // indexar, pero lo que el jugador vio cantar es ese número más uno.
            dice.Block(raw % bagSize, (raw + 1).ToString());
            return AIResult.Succeeded;
        }

        // -- Boss 2: prohíbe los últimos N combos del log (memoria de combos) --------
        private AIResult RotateCombo(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IContractModifierService>(out var mods) || mods == null)
            {
                Debug.LogError("[AINode_RotateBlock] IContractModifierService no registrado. " +
                               "Agrega ContractModifierServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }
            if (!ServiceLocator.TryGetService<IComboLogService>(out var log) || log == null)
            {
                Debug.LogError("[AINode_RotateBlock] IComboLogService no registrado. " +
                               "Agrega ComboLogServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            // Ventana deslizante: se descarta lo prohibido el turno previo y se prohíben los últimos
            // N combos (ver CombatHandoffService.DetectWithContractMods).
            mods.ClearAll();

            var recent = log.Last(Count);
            if (recent.Count == 0) return AIResult.Succeeded; // Turno 1: nada que repetir todavía.

            foreach (var comboId in recent)
                mods.ForbidCombo(comboId);

            return AIResult.Succeeded;
        }

        // -- Helpers ------------------------------------------------------------------

        private static int ResolveBagSize(AIContext context)
        {
            var ps = context.PlayerService;
            if (ps == null) ServiceLocator.TryGetService<IPlayerService>(out ps);
            int size = ps?.DiceBag?.Dice?.Count ?? 0;
            if (size <= 0)
                Debug.LogWarning("[AINode_RotateBlock] No se pudo resolver el tamaño de la build (DiceBag). No se bloquea ningún dado.");
            return size;
        }

        private static int NextInt(AIContext context, int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 1) return 0;
            return context.Rng != null
                ? context.Rng.Next(exclusiveUpperBound)
                : UnityEngine.Random.Range(0, exclusiveUpperBound);
        }
    }
}
