using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Sólo elige los números: marcar el sector y confiscar el dado son otros dos nodos que leen de
    /// acá. <see cref="CantoFeedbackId"/> vacío ⇒ el id canónico: Odin no corre field initializers,
    /// así que un <c>ED_Boss_Croupier</c> ya autorado no trae el campo.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SpinWheel : AIActionNode, IAIOpeningNode
    {
        [Tooltip("Daño de la Represalia de mesa: lo que cuesta pegarle. Se cobra siempre — cualquier " +
                 "número, cualquier fase, con o sin windup abierto. Es su único daño directo.")]
        [MinValue(0)]
        public int RetaliationDamage = 8;

        [Tooltip("Si está activo, nunca canta dos veces seguidas el mismo número: el paño se mueve " +
                 "todos los turnos. Apagalo para dejar que el azar repita.")]
        public bool AvoidRepeatingLastNumber = true;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Animación del canto. Vacío = el id canónico del Croupier (ver remarks).")]
        public string CantoFeedbackId = BossFeedbackIds.CroupierCantoAnim;

        [Tooltip("Event key del Animation Event en el que el número aparece sobre el jefe. El clip de " +
                 "ataque del rig Healer publica 'cast'. Vacío = el número se publica al terminar la " +
                 "animación.")]
        public string CantoEventKey;

        [NonSerialized] private int _lastNumber;

        public override string NodeName => "Spin Wheel (Croupier)";

        /// <summary>Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): bloquear acá colgaría los tests.</summary>
        public override AIResult Tick(AIContext context)
        {
            if (!TryPrepare(context, out var wheel, out var numbers)) return AIResult.Failed;

            Sing(wheel, numbers);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Canta por el camino síncrono antes del primer turno: el nodo de confiscación lee el número
        /// y <c>Bind</c> —que pasa por acá— instala la Represalia. Sin animación: la apertura corre
        /// dentro del armado de la cola y retener el turno ahí cuelga el arranque del combate.
        /// </summary>
        public void Opening(AIContext context) => Tick(context);

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (!TryPrepare(context, out var wheel, out var numbers))
            {
                onResult?.Invoke(AIResult.Failed);
                yield break;
            }

            bool sung = false;
            Action singOnce = () =>
            {
                if (sung) return;
                sung = true;
                Sing(wheel, numbers);
            };

            var canto = PlayCanto(context, singOnce);
            while (canto.MoveNext()) yield return canto.Current;

            // Red de seguridad: el número igual sale — los nodos que siguen lo dan por hecho.
            singOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        private bool TryPrepare(AIContext context, out ICroupierWheelService wheel, out List<int> numbers)
        {
            wheel = null;
            numbers = null;
            if (context == null || context.SelfGuid == Guid.Empty) return false;

            wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return false;

            wheel.Bind(context.SelfGuid);
            wheel.RetaliationDamage = RetaliationDamage;

            numbers = PickNumbers(context, wheel.NumbersPerTurn);
            return numbers.Count > 0;
        }

        private void Sing(ICroupierWheelService wheel, List<int> numbers)
        {
            wheel.Sing(numbers);
            _lastNumber = numbers[0];
        }

        private IEnumerator PlayCanto(AIContext context, Action onCanto)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            // El step bloquea la duración COMPLETA de la entry, no hasta el evento del canto: el
            // número puede salir en el gesto y el turno igual se retiene hasta que el clip termina.
            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = string.IsNullOrEmpty(CantoFeedbackId)
                    ? BossFeedbackIds.CroupierCantoAnim
                    : CantoFeedbackId,
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

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool cantoFired = string.IsNullOrEmpty(CantoEventKey);

            // El bus es latched: pollear HasFired por frame engancha el Animation Event.
            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext())
            {
                if (!cantoFired)
                {
                    var bus = FeedbackSequenceRuntime.Current;
                    if (bus != null && bus.HasFired(CantoEventKey))
                    {
                        cantoFired = true;
                        onCanto?.Invoke();
                    }
                }
                yield return wait.Current;
            }
        }

        /// <summary><paramref name="count"/> números distintos entre sí de 1..6: dos iguales en fase 2 harían caer un solo sector.</summary>
        private List<int> PickNumbers(AIContext context, int count)
        {
            int total = ThreatAreaShape.RoomSectorCount;
            var pool = new List<int>(total);
            for (int n = 1; n <= total; n++)
            {
                // Descarte del pool y no re-sorteo: así los números restantes quedan equiprobables.
                if (AvoidRepeatingLastNumber && n == _lastNumber && total > 1) continue;
                pool.Add(n);
            }

            int take = count < 1 ? 1 : count;
            if (take > pool.Count) take = pool.Count;

            var picked = new List<int>(take);
            for (int i = 0; i < take; i++)
            {
                int j = NextInt(context, pool.Count);
                picked.Add(pool[j]);
                pool.RemoveAt(j);
            }
            return picked;
        }

        private static int NextInt(AIContext context, int exclusiveUpperBound)
        {
            if (exclusiveUpperBound <= 1) return 0;
            return context.Rng != null
                ? context.Rng.Next(exclusiveUpperBound)
                : UnityEngine.Random.Range(0, exclusiveUpperBound);
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
