using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.ContractMod;
using Rollgeon.Combat.DiceBlock;
using Rollgeon.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Recalcula el "estado bloqueado rotativo" del Boss al final de su turno (Sistemas
    /// prerequisito Bosses §5; decisión de diseño: el boss computa al cerrar su turno y el
    /// jugador lo ve al iniciar el suyo). Dos modos:
    /// <list type="bullet">
    ///   <item><description><b>Dice</b> (Boss 1): sortea <see cref="Count"/> dados distintos al
    ///   azar de la build y los bloquea vía <see cref="IDiceBlockService"/>.</description></item>
    ///   <item><description><b>Combo</b> (Boss 2): lee los últimos <see cref="Count"/> combos del
    ///   <see cref="IComboLogService"/> y los <b>prohíbe</b> vía <see cref="IContractModifierService"/>
    ///   (ventana deslizante: <c>ClearAll</c> + <c>ForbidCombo</c>). Un combo prohibido aparece con
    ///   daño 0 en la UI del Contrato y, si el jugador lo arma, hace 0 daño.</description></item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// <b>Fases (ad-hoc).</b> La diferencia Fase 1 (1) vs Fase 2 (2) se modela en el árbol con un
    /// <c>AINode_If(PcOwnerHpBelow)</c> que ramifica a dos instancias de este nodo con
    /// <see cref="Count"/> distinto — no hay mutación de estado en runtime.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_RotateBlock : AIActionNode
    {
        /// <summary>Qué se bloquea: dados (Boss 1) o combos del log (Boss 2).</summary>
        public enum BlockTarget { Dice, Combo }

        [Tooltip("Dice = Boss 1 (sortea dados). Combo = Boss 2 (bloquea los últimos N combos del log).")]
        public BlockTarget Target = BlockTarget.Dice;

        [Tooltip("Cuántos dados sortear (Boss 1) o tamaño de la ventana de combos (Boss 2). Fase 1 = 1, Fase 2 = 2.")]
        [MinValue(1)]
        public int Count = 1;

        public override string NodeName => $"Rotate Block ({Target} ×{Count})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            return Target == BlockTarget.Dice ? RotateDice(context) : RotateCombo(context);
        }

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

            // Ventana deslizante: descartamos lo prohibido el turno previo y prohibimos los
            // últimos N combos. El combo prohibido se muestra con daño 0 en el Contrato y, si
            // el jugador lo arma, hace 0 daño (ver CombatHandoffService.DetectWithContractMods).
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
