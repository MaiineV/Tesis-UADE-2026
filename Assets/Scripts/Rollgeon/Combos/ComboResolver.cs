using System.Collections.Generic;

namespace Rollgeon.Combos
{
    /// <summary>
    /// Helper estático para detectar el combo de mayor prioridad sobre una tirada.
    /// Reusado por <c>DiceZoneView</c> (UI HUD), <c>ActionRollService</c> (Force Door /
    /// Heal) y cualquier consumer que necesite resolver "el mejor combo" sin reinventar
    /// el loop sobre <see cref="ComboCatalogSO"/>.
    /// </summary>
    public static class ComboResolver
    {
        /// <summary>
        /// Recorre <paramref name="catalog"/> y devuelve el combo de mayor
        /// <see cref="BaseComboSO.Priority"/> que matchee <paramref name="dice"/>.
        /// </summary>
        /// <param name="best">El combo ganador, o <c>null</c> si ninguno matchea.</param>
        /// <returns>El <see cref="ComboDetectionResult"/> del combo ganador, o
        /// <see cref="ComboDetectionResult.NoMatch"/> si ninguno matchea / catalog null.</returns>
        public static ComboDetectionResult DetectBest(ComboCatalogSO catalog,
            IReadOnlyList<int> dice, out BaseComboSO best)
        {
            best = null;
            if (catalog == null || dice == null || dice.Count == 0)
                return ComboDetectionResult.NoMatch();

            var diceArr = dice as int[] ?? CopyToArray(dice);

            ComboDetectionResult bestResult = ComboDetectionResult.NoMatch();
            int bestPriority = int.MinValue;

            foreach (var combo in catalog.Entries)
            {
                if (combo == null) continue;
                var result = combo.Detect(diceArr);
                if (result.IsMatch && combo.Priority > bestPriority)
                {
                    bestPriority = combo.Priority;
                    bestResult = result;
                    best = combo;
                }
            }

            return bestResult;
        }

        private static int[] CopyToArray(IReadOnlyList<int> source)
        {
            var arr = new int[source.Count];
            for (int i = 0; i < source.Count; i++) arr[i] = source[i];
            return arr;
        }
    }
}
