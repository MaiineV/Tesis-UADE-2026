using System.Linq;
using Patterns;
using Rollgeon.Combos.Rules;
using UnityEngine;

namespace Rollgeon.Combos.Concretes
{
    /// <summary>
    /// Escalera — cinco valores consecutivos (orden-agnostico). <c>CountUsed = 5</c>. Base del GD: 35.
    /// <para>
    /// Acepta <c>[1,2,3,4,5]</c>, <c>[2,3,4,5,6]</c> y para d8+ futuro <c>[3,4,5,6,7]</c>, etc.
    /// Normalizacion interna (plan §5.4): <c>Distinct().OrderBy()</c>. Test §9.2 cubre orden mezclado.
    /// </para>
    /// <para>
    /// <b>Compás Salteado</b> (<see cref="IComboRuleService.LadderAllowsSkippedStep"/>): con la
    /// regla activa cada salto entre valores consecutivos puede ser 1 o 2, mezclados
    /// (<c>[3,5,7,9,11]</c>, <c>[2,4,6,8,10]</c>, <c>[5,7,9,10,11]</c> — decisión GD 2026-09-03:
    /// la escalera "mixta" también vale). Sigue siendo el mismo combo — mismo id, mismo base,
    /// mismas pasivas de Escalera. Sin servicio registrado rige la regla estándar, así que el
    /// preview del HUD y el golpe real (ambos pasan por acá) siempre coinciden.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combos/Escalera", fileName = "Combo_Escalera")]
    public class Combo_Escalera : BaseComboSO
    {
        private const int StraightLength = 5;
        private const int SkippedStep = 2;

        /// <inheritdoc />
        public override bool Matches(int[] finalDice)
        {
            if (finalDice == null || finalDice.Length < StraightLength) return false;
            var distinct = finalDice.Distinct().OrderBy(d => d).ToArray();
            if (distinct.Length != StraightLength) return false;
            if (HasStepsWithin(distinct, 1, 1)) return true;
            return LadderAllowsSkippedStep() && HasStepsWithin(distinct, 1, SkippedStep);
        }

        // Cada salto entre vecinos ordenados tiene que caer en [minStep, maxStep]; con
        // min == max es la regla estándar de paso constante.
        private static bool HasStepsWithin(int[] sorted, int minStep, int maxStep)
        {
            for (int i = 1; i < sorted.Length; i++)
            {
                int step = sorted[i] - sorted[i - 1];
                if (step < minStep || step > maxStep) return false;
            }
            return true;
        }

        private static bool LadderAllowsSkippedStep()
            => ServiceLocator.TryGetService<IComboRuleService>(out var rules)
               && rules != null
               && rules.LadderAllowsSkippedStep;

        /// <inheritdoc />
        protected override int GetCountUsed(int[] finalDice) => StraightLength;
    }
}
