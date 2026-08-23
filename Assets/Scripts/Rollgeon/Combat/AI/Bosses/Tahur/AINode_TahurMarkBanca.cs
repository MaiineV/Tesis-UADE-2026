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
    /// Va último en el turno, después del movimiento y de poner la mesa: el hueco se ancla en el
    /// jefe. Marca sobre el guid del jefe, el mismo canal que <see cref="AINode_TahurSettleWager"/>
    /// —y <see cref="IThreatenedAreaService.Mark"/> sobrescribe—, así que el Castigo y La Banca
    /// nunca detonan juntos. Puede devolver <c>Failed</c>: va en <c>Selector[Banca, Wait]</c>.
    /// </summary>
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

        /// <summary>Vacío significa "el id canónico", no "sin animación": Odin deserializa un <c>ED_Boss_*.asset</c> viejo sin correr los field initializers.</summary>
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

            // El hueco es La Mesa, no un cuadrado parecido: si TableRadius y el Size divergen, gana el paño.
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

            // La ronda queda contada como ronda con marca: el poke es exclusivo de la rama limpia.
            wager.ReportOutcome(wager.LastOutcome, markedPunishment: true);
            return AIResult.Succeeded;
        }

        /// <summary>Marca primero y <b>después</b> barre. La Banca no golpea este turno: el daño cae en el siguiente por el <c>AINode_ExecuteTelegraph</c>.</summary>
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

            if (turn == null || !turn.IsWaitingForFeedback) yield break;

            var wait = TurnManager.WaitForFeedbackCompletion(turn);
            while (wait.MoveNext()) yield return wait.Current;
        }

        /// <summary>Fichas a partir de las cuales barre la mesa, clampeado a <see cref="ITahurWagerService.MaxChips"/>.</summary>
        public int EffectiveThreshold(ITahurWagerService wager)
        {
            int threshold = Mathf.Max(1, ChipsThreshold);
            if (wager == null) return threshold;
            return Mathf.Min(threshold, Mathf.Max(1, wager.MaxChips));
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
