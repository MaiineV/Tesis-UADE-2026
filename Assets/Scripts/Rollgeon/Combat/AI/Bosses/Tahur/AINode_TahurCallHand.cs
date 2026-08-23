using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.ContractMod;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurCallHand : AIActionNode
    {
        [Title("El canto")]
        [Tooltip("Escalón más bajo que puede cantar (1 = la peor mano del contrato).")]
        [MinValue(1)]
        public int MinRank = 1;

        [Tooltip("Escalón más alto que puede cantar. Se clampea a la cantidad de manos del contrato.")]
        [MinValue(1)]
        public int MaxRank = 6;

        [Tooltip("Desde qué escalón un canto cuenta como 'alto' para la válvula.")]
        [MinValue(1)]
        public int HighRankThreshold = 5;

        [Tooltip("La válvula: nunca dos cantos altos seguidos. Sin esto, dos rondas de escalón 6 " +
                 "encadenadas piden armar la mano máxima dos veces y el jefe deja de ser legible.")]
        public bool AvoidConsecutiveHighCalls = true;

        [Tooltip("Rota con memoria: no repite un escalón hasta agotar el conjunto. False = sorteo libre.")]
        public bool UseRotationMemory = true;

        [Title("Reglas del Contrato")]
        [Tooltip("Prohibir la mano cantada (R03): armarla hace 0 — el precio de cobrar el pozo.")]
        public bool ForbidCalledHand = true;

        [Tooltip("Multiplicador de la codicia (R01) sobre las manos por encima del escalón a armar.")]
        [MinValue(1f)]
        public float GreedMultiplier = 2f;

        [Tooltip("Cancelar las reglas de la ronda anterior antes de cantar. En off las reglas se " +
                 "acumularían ronda a ronda y el Contrato quedaría ilegible.")]
        public bool ClearPreviousRules = true;

        // Estado por pelea: el árbol se clona por combate, así que se resetea solo.
        [NonSerialized] private List<int> _calledSinceRefill;

        public override string NodeName => "Tahúr — Call Hand (canta el escalón)";

        public IReadOnlyList<int> CalledSinceRefill => Used;

        private List<int> Used => _calledSinceRefill ??= new List<int>();

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            var ladder = TahurHandLadder.FromContext(context);
            if (!ladder.IsValid)
            {
                Debug.LogWarning("[AINode_TahurCallHand] Sin ContractSheet del jugador — no hay qué cantar.");
                return AIResult.Failed;
            }

            var wager = TahurWagerService.ResolveOrCreate();

            int minRank = Mathf.Max(1, MinRank);
            // LEE: el cobro es el escalón de abajo, así que el 1 no se puede cantar.
            if (wager.CallInverted) minRank = Mathf.Max(2, minRank);
            int maxRank = Mathf.Min(MaxRank <= 0 ? ladder.Count : MaxRank, ladder.Count);
            if (maxRank < minRank) return AIResult.Failed;

            int previousCall = wager.CalledRank;
            var pool = BuildPool(minRank, maxRank, previousCall, useRotation: UseRotationMemory, useValve: true);

            // Conjunto agotado ⇒ refill (la rotación arranca de nuevo, no se rompe).
            if (pool.Count == 0 && UseRotationMemory)
            {
                Used.Clear();
                pool = BuildPool(minRank, maxRank, previousCall, useRotation: true, useValve: true);
            }
            // La válvula cede antes que quedarse sin cantar.
            if (pool.Count == 0)
                pool = BuildPool(minRank, maxRank, previousCall, useRotation: false, useValve: true);
            if (pool.Count == 0)
                pool = BuildPool(minRank, maxRank, previousCall, useRotation: false, useValve: false);
            if (pool.Count == 0) return AIResult.Failed;

            int rank = pool[NextInt(context, pool.Count)];
            string calledComboId = ladder.ComboIdAt(rank);
            if (string.IsNullOrEmpty(calledComboId)) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<IContractModifierService>(out var mods) || mods == null)
            {
                Debug.LogError("[AINode_TahurCallHand] IContractModifierService no registrado. " +
                               "Agrega ContractModifierServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            if (ClearPreviousRules) mods.ClearAll();
            if (ForbidCalledHand) mods.ForbidCombo(calledComboId);

            wager.SetCall(rank, calledComboId);
            Used.Add(rank);

            // Codicia: ×2 a todo lo que esté por encima del escalón que hay que armar.
            int targetRank = wager.TargetRank;
            for (int r = targetRank + 1; r <= ladder.Count; r++)
            {
                if (ForbidCalledHand && r == rank) continue;
                var comboId = ladder.ComboIdAt(r);
                if (!string.IsNullOrEmpty(comboId)) mods.MultiplyCombo(comboId, GreedMultiplier);
            }

            return AIResult.Succeeded;
        }

        private List<int> BuildPool(int minRank, int maxRank, int previousCall, bool useRotation, bool useValve)
        {
            bool banHighCalls = useValve && AvoidConsecutiveHighCalls
                                         && previousCall >= Mathf.Max(1, HighRankThreshold);

            var pool = new List<int>(Mathf.Max(0, maxRank - minRank + 1));
            for (int rank = minRank; rank <= maxRank; rank++)
            {
                if (banHighCalls && rank >= Mathf.Max(1, HighRankThreshold)) continue;
                if (useRotation && Used.Contains(rank)) continue;
                pool.Add(rank);
            }
            return pool;
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
