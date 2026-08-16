using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// "La Banca" del Tahúr: con el pozo lleno se levanta y barre la mesa — marca toda la sala
    /// menos La Mesa, su 3×3, y cobra 45 al turno siguiente. Ficha de diseño "El Tahúr" (piso 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Para qué existe.</b> Es la línea que le saca al jugador la salida de no jugar. Sin ella,
    /// renunciar al pozo era gratis: el Castigo se esquiva caminando y el poke sólo entra si vas a
    /// cobrar, así que quedarse lejos era una postura estable. Con el rastrillo corriendo desde la
    /// fase 1 (<see cref="AINode_TahurSettleWager.RakeChipsPerRound"/>) el pozo se llena solo, y
    /// cuando se llena la única casilla que no cobra es justo la que exige jugar su juego.
    /// </para>
    /// <para>
    /// <b>Va último en el turno, después del movimiento y de poner la mesa.</b> El hueco se ancla
    /// en el jefe, así que marcarlo antes de moverse lo dejaría centrado donde ya no está: el paño
    /// cian diría una cosa y el hueco seguro otra. Marcado al final, el hueco y La Mesa son el
    /// mismo 3×3 y siguen siéndolo hasta que detona, porque el jefe no se mueve entre el final de
    /// su turno y el <c>AINode_ExecuteTelegraph</c> que abre el siguiente.
    /// </para>
    /// <para>
    /// <b>Reemplaza al Castigo de la ronda.</b> Marca sobre el guid del jefe, el mismo canal que
    /// usa <see cref="AINode_TahurSettleWager"/>, y <see cref="IThreatenedAreaService.Mark"/>
    /// sobrescribe: nunca detonan los dos. Es lo correcto además de lo cómodo — 45 + 45 rompería
    /// el techo de daño por golpe del piso 3, y con el pozo lleno el Castigo ya valía 45.
    /// </para>
    /// <para>
    /// <b>El poke no se le suma.</b> El poke tiene alcance 1 Manhattan desde el jefe, así que todo
    /// lo que puede alcanzar está dentro del 3×3 — o sea, dentro del hueco. Quien cobra los 45 está
    /// fuera de rango del poke por construcción, y quien recibe el poke cobró 0 de La Banca.
    /// </para>
    /// <para>
    /// Devuelve <see cref="AIResult.Failed"/> cuando el pozo todavía no está lleno (el caso normal)
    /// ⇒ va envuelto en <c>Selector[Banca, Wait]</c> como el resto de los nodos que pueden fallar.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurMarkBanca : AIActionNode
    {
        [Title("El disparador")]
        [Tooltip("Fichas que tiene que tener el pozo para que la banca barra la mesa. 5 = pozo lleno.")]
        [MinValue(1)]
        public int ChipsThreshold = 5;

        [Title("Daño")]
        [Tooltip("Daño de La Banca al detonar el turno siguiente.")]
        [MinValue(0)]
        public int Damage = 45;

        [Tooltip("Techo duro de daño por golpe del piso 3. La Banca nunca pega más que esto.")]
        [MinValue(0)]
        public int DamageCeiling = 45;

        [Tooltip("Tipo de ataque de La Banca al detonar.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Title("El hueco")]
        [Tooltip("Radio del hueco seguro (1 ⇒ el 3×3 de La Mesa). Tiene que ser el mismo Size que " +
                 "AINode_TahurMarkTable: el hueco y el paño cian son la misma promesa.")]
        [MinValue(0)]
        public int TableRadius = 1;

        [Title("Presentación")]
#if UNITY_EDITOR
        [ValueDropdown(nameof(GetFeedbackIdsForDropdown))]
#endif
        [Tooltip("Override del gesto de barrer la mesa. Vacío = " + BossFeedbackIds.TahurBancaAnim + ".")]
        public string AnimFeedbackIdOverride;

        public override string NodeName => $"Tahúr — La Banca ({Damage} en toda la sala menos La Mesa)";

        /// <remarks>
        /// Vacío significa "el id canónico del nodo", no "sin animación": Odin puede deserializar
        /// un <c>ED_Boss_*.asset</c> viejo sin correr los field initializers, así que un default en
        /// el campo llegaría en null y el barrido volvería a marcarse sin que el jefe se mueva.
        /// </remarks>
        private string AnimFeedbackId => string.IsNullOrEmpty(AnimFeedbackIdOverride)
            ? BossFeedbackIds.TahurBancaAnim
            : AnimFeedbackIdOverride;

        public override AIResult Tick(AIContext context)
        {
            if (context?.Grid == null) return AIResult.Failed;

            var wager = TahurWagerService.ResolveOrCreate();
            if (wager.Chips < EffectiveThreshold(wager)) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;

            var tiles = ThreatAreaShape.Compute(
                context.Grid, selfCoord, ThreatShape.AllExceptSquareAroundSelf,
                TableRadius, HalfRoomAxis.Vertical);

            // El hueco es La Mesa, no un cuadrado parecido: si el radio de acá y el Size del nodo
            // de la mesa alguna vez divergen, la promesa que tiene que sobrevivir es la del paño
            // cian — es la única que el jugador puede leer en pantalla.
            tiles.ExceptWith(wager.TableTiles);

            if (tiles.Count == 0)
            {
                Debug.LogWarning("[AINode_TahurMarkBanca] La Banca no cubrió ninguna casilla — " +
                                 "¿sala más chica que La Mesa, o grafo sin bounds? No se marca nada.");
                return AIResult.Failed;
            }

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_TahurMarkBanca] IThreatenedAreaService no registrado. " +
                               "Agrega ThreatenedAreaServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            threat.Mark(context.SelfGuid, tiles, Mathf.Clamp(Damage, 0, DamageCeiling), Kind);
            ThreatTelegraphOverlay.ResolveOrCreate()
                .Show(context.SelfGuid, tiles, ThreatOverlayState.Marked);

            // La ronda queda contada como ronda con marca aunque el poke ya haya pasado: si alguien
            // reordena el turno y el poke termina después, el gate de PcTahurCleanRound lo ataja.
            wager.ReportOutcome(wager.LastOutcome, markedPunishment: true);
            return AIResult.Succeeded;
        }

        /// <summary>
        /// Camino de play mode: marca primero y <b>después</b> barre, reteniendo el turno hasta que
        /// el gesto termina.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El orden importa: el rastrillo tiene que estar pintado mientras el jefe se levanta, o el
        /// brazo barre una sala vacía y el gesto no explica de dónde salen los 45. Y como el nodo va
        /// último en el turno, retener acá no le roba tiempo a nada — es el cierre.
        /// </para>
        /// <para>
        /// <b>Sólo la animación, sin impacto.</b> La Banca no golpea este turno: los 45 caen en el
        /// siguiente por el <c>AINode_ExecuteTelegraph</c>, que trae su propio windup y su propio
        /// impacto. Meter VFX de golpe acá prometería un daño que todavía no existe.
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

            var beat = PlaySweep(context);
            while (beat.MoveNext()) yield return beat.Current;

            onResult?.Invoke(result);
        }

        /// <remarks>
        /// El request se arma a mano en vez de reusar <c>EffPlaySequence</c>: el nodo no nace de un
        /// effect pass, así que no tiene <c>EffectContext</c> que pasarle (mismo caso que la
        /// secuencia de muerte del <c>CombatDeathWatcher</c>, y por eso <c>FeedbackRequest.Context</c>
        /// admite null).
        /// </remarks>
        private IEnumerator PlaySweep(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IFeedbackService>(out var feedback) || feedback == null) yield break;

            var step = new FeedbackSequenceStep
            {
                Source = StepSource.FeedbackRef,
                FeedbackRefId = AnimFeedbackId,
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

            // Sin TurnManager no hay gate que esperar — la anim igual corre, pero el turno no se
            // retiene. Mismo degradado que EffPlaySequence.
            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        /// <summary>
        /// Fichas a partir de las cuales barre la mesa, sin poder quedar por encima del techo del
        /// pozo.
        /// </summary>
        /// <remarks>
        /// El pozo está clampeado a <see cref="ITahurWagerService.MaxChips"/>: un umbral por encima
        /// de ese techo dejaría el nodo muerto sin que nada lo cante. "Pozo lleno" es la condición
        /// de la ficha, y cuánto es lleno lo decide la banca.
        /// </remarks>
        public int EffectiveThreshold(ITahurWagerService wager)
        {
            int threshold = Mathf.Max(1, ChipsThreshold);
            if (wager == null) return threshold;
            return Mathf.Min(threshold, Mathf.Max(1, wager.MaxChips));
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
