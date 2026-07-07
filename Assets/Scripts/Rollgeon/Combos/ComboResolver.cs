using System.Collections.Generic;

namespace Rollgeon.Combos
{
    /// <summary>
    /// Helper estático para detectar el combo de mayor prioridad sobre una tirada.
    /// Reusado por <c>DiceZoneView</c> (UI HUD), <c>ActionRollService</c> (Force Door /
    /// Heal) y cualquier consumer que necesite resolver "el mejor combo" sin reinventar
    /// el loop sobre <see cref="ComboCatalogSO"/>.
    /// </summary>
    /// <remarks>
    /// <b>Spec de Daño v2 (Santi) — decisión de scope.</b> El spec pide "elegir el combo con
    /// mayor dmg, no el primero detectado". Evaluamos esto y decidimos <b>no</b> cambiar el
    /// criterio de selección acá ni en <c>ContractSheet.MatchBest</c> (el path real en
    /// combate — este resolver es solo fallback defensivo). Motivos:
    /// <list type="bullet">
    /// <item><description><c>ContractSheet.Validate</c> exige que la última entrada del
    /// contrato sea Generala con <c>Priority == int.MaxValue</c> — es una regla dura
    /// validada por asset. Reordenar por dmg total la vuelve inconsistente/inútil.</description></item>
    /// <item><description>El bono de encantamientos de dado (Gemelo/Par-Impar vía
    /// <c>IDiceEnchantmentService.LastComboScratch</c>) se calcula reactivamente DESPUÉS de
    /// elegir el combo ganador (dispara triggers con side effects, ej. gastar oro) — no es
    /// posible evaluarlo "por candidato" antes de decidir sin re-disparar esos efectos.</description></item>
    /// <item><description>Elegir por dmg exigiría enhebrar <c>DiceType</c> (no solo el valor
    /// de cara) por <c>ContractSheet</c>, <c>ActionRollService</c> y la UI — blast radius
    /// mucho mayor al de aplicar la fórmula v2 solo sobre el combo ya elegido.</description></item>
    /// </list>
    /// La fórmula v2 completa (multi_dmg_combo + bono_combo bien ordenados) se aplica en
    /// <see cref="Rollgeon.Combat.Damage.PlayerComboDamage"/> sobre el combo que esta clase
    /// ya eligió por <c>Priority</c> — sin cambios acá.
    /// </remarks>
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
