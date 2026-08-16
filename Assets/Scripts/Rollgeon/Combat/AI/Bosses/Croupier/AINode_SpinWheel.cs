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
    /// "Hagan sus apuestas": el Croupier canta <see cref="ICroupierWheelService.NumbersPerTurn"/>
    /// número(s) del 1 al 6 y los deja flotando sobre él. Cada número es dos cosas a la vez — el
    /// sector del paño que va a caer el turno que viene y el dado de la bolsa que se confisca — así
    /// que este nodo no hace nada más que elegirlo: marcar el sector y confiscar el dado son otros dos
    /// nodos que leen de acá.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va inmediatamente antes del nodo de confiscación y del de marcado en el Sequence raíz. Abre el
    /// windup: desde que este nodo corre hasta que el sector detona, cerrar el turno dentro del sector
    /// cantado corre la rueda.
    /// </para>
    /// <para>
    /// <b>El canto es el aviso.</b> Toda la pelea cuelga de que el jugador lea el número a tiempo, y
    /// hasta ahora el número aparecía solo, sin que el jefe hiciera nada: era una etiqueta de UI, no
    /// un acto del Croupier. La animación se corre en el camino coroutine y <b>retiene el turno</b>
    /// hasta terminar, para que el marcado del sector no empiece a pintar tiles mientras el jefe
    /// todavía está cantando.
    /// </para>
    /// <para>
    /// <b>Elegir y publicar están separados.</b> El sorteo pasa antes de la animación (si no hay
    /// número que cantar tampoco hay que animar nada ni retener el turno) y la publicación pasa en el
    /// frame del canto — o al terminar, si el clip no publica su key. Un canto que ya mostró el número
    /// antes de arrancar se lee como si el jefe reaccionara a la rueda en vez de moverla.
    /// </para>
    /// <para>
    /// <b><see cref="CantoFeedbackId"/> vacío ⇒ el id canónico del jefe</b>
    /// (<see cref="BossFeedbackIds"/>). No es una comodidad: Odin instancia el nodo sin correr field
    /// initializers y los <c>ED_Boss_Croupier</c> ya autorados no traen el campo, así que un default
    /// por inicializador nunca llegaría al asset y el canto seguiría mudo. Para silenciarlo se lo
    /// apunta a otra entry, no se lo vacía.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_SpinWheel : AIActionNode
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
                 "animación, que es el degradado correcto: el timing es una preferencia, no la mecánica.")]
        public string CantoEventKey;

        [NonSerialized] private int _lastNumber;

        public override string NodeName => "Spin Wheel (Croupier)";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>): sortea y publica en el
        /// mismo tick. No hay dónde esperar una animación, y bloquear acá colgaría los tests.
        /// </summary>
        public override AIResult Tick(AIContext context)
        {
            if (!TryPrepare(context, out var wheel, out var numbers)) return AIResult.Failed;

            Sing(wheel, numbers);
            return AIResult.Succeeded;
        }

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

            // Red de seguridad: sin feedback service, con un id huérfano o con el watchdog cortando la
            // secuencia, el número igual sale. Los nodos que siguen en el Sequence lo dan por hecho.
            singOnce();
            onResult?.Invoke(AIResult.Succeeded);
        }

        // ---- pasos compartidos por los dos caminos -------------------------

        /// <summary>
        /// Resuelve el servicio y sortea los números, sin publicar nada. Separado de
        /// <see cref="Sing"/> para que el camino coroutine pueda fallar <b>antes</b> de animar: un
        /// canto sin número que cantar retendría el turno para no decir nada.
        /// </summary>
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

        /// <remarks>
        /// Request de secuencia armado a mano en vez de un <c>EffPlaySequence</c>: el nodo no nace de
        /// un effect pass, así que no tiene <c>EffectContext</c> que pasarle (mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null).
        /// </remarks>
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

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el número no queda
            // sincronizado. Mismo degradado que EffPlaySequence.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            bool cantoFired = string.IsNullOrEmpty(CantoEventKey);

            // Se envuelve el wait canónico (trae su propio timeout + force-reset del depth) en vez de
            // rehacer el loop: el bus es latched, así que pollear HasFired por frame alcanza para
            // enganchar el Animation Event sin suscribirse a nada.
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

        /// <summary>
        /// <paramref name="count"/> números distintos entre sí de 1..6. Distintos porque dos números
        /// iguales en fase 2 harían caer un solo sector y el turno se leería como fase 1.
        /// </summary>
        private List<int> PickNumbers(AIContext context, int count)
        {
            int total = ThreatAreaShape.RoomSectorCount;
            var pool = new List<int>(total);
            for (int n = 1; n <= total; n++)
            {
                // El descarte del número anterior es del pool, no un re-sorteo: así todos los números
                // restantes quedan equiprobables en vez de sesgar hacia el segundo intento.
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
