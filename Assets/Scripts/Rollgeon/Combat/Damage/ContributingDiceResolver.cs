using System.Collections.Generic;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Mapea <see cref="Rollgeon.Combos.ComboDetectionResult.ContributingIndices"/> (relativos
    /// al subset de dados holdeado) de vuelta al <see cref="DiceType"/> real de cada dado en el
    /// bag del jugador. Compartido por <c>EffDealDamage</c> (resolución real de daño) y
    /// <c>DiceZoneView</c> (preview de <c>multi_dmg_combo</c> en el HUD) para no duplicar la
    /// lógica de mapeo índice↔slot.
    /// </summary>
    public static class ContributingDiceResolver
    {
        /// <param name="contributingIndices">Índices relativos al subset holdeado.</param>
        /// <param name="keptDiceOriginalIndices">Mapeo subset→bag slot (ver
        /// <c>EffectContext.KeptDiceOriginalIndices</c>). Null ⇒ se asume que
        /// <paramref name="contributingIndices"/> ya son bag slots directos (ej. cuando no
        /// hubo filtrado de holds).</param>
        /// <param name="bagDice">Tipos de dado del bag, en orden de slot.</param>
        public static IReadOnlyList<DiceType> Resolve(
            IReadOnlyList<int> contributingIndices,
            IReadOnlyList<int> keptDiceOriginalIndices,
            IReadOnlyList<DiceType> bagDice)
        {
            if (contributingIndices == null || contributingIndices.Count == 0 || bagDice == null)
                return null;

            var result = new List<DiceType>(contributingIndices.Count);
            for (int i = 0; i < contributingIndices.Count; i++)
            {
                int localIndex = contributingIndices[i];
                int bagSlot = (keptDiceOriginalIndices != null
                                && localIndex >= 0 && localIndex < keptDiceOriginalIndices.Count)
                    ? keptDiceOriginalIndices[localIndex]
                    : localIndex;
                if (bagSlot < 0 || bagSlot >= bagDice.Count) continue;
                result.Add(bagDice[bagSlot]);
            }
            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Variante detallada de <see cref="Resolve"/>: además del tipo conserva el bag slot
        /// y la cara tirada de cada dado contribuyente. La fórmula v3 suma las caras y la UI
        /// de breakdown necesita el slot para animar el dado físico.
        /// </summary>
        /// <param name="contributingIndices">Índices relativos al subset holdeado.</param>
        /// <param name="keptDiceOriginalIndices">Mapeo subset→bag slot. Null ⇒ identidad
        /// (los índices ya son bag slots directos).</param>
        /// <param name="keptFaces">Caras del subset holdeado, alineadas con
        /// <paramref name="contributingIndices"/> (mismo espacio de índices locales).</param>
        /// <param name="bagDice">Tipos de dado del bag, en orden de slot.</param>
        public static IReadOnlyList<ContributingDie> ResolveDetailed(
            IReadOnlyList<int> contributingIndices,
            IReadOnlyList<int> keptDiceOriginalIndices,
            IReadOnlyList<int> keptFaces,
            IReadOnlyList<DiceType> bagDice)
        {
            if (contributingIndices == null || contributingIndices.Count == 0 || bagDice == null)
                return null;

            var result = new List<ContributingDie>(contributingIndices.Count);
            for (int i = 0; i < contributingIndices.Count; i++)
            {
                int localIndex = contributingIndices[i];
                int bagSlot = (keptDiceOriginalIndices != null
                                && localIndex >= 0 && localIndex < keptDiceOriginalIndices.Count)
                    ? keptDiceOriginalIndices[localIndex]
                    : localIndex;
                if (bagSlot < 0 || bagSlot >= bagDice.Count) continue;
                if (keptFaces == null || localIndex < 0 || localIndex >= keptFaces.Count) continue;
                result.Add(new ContributingDie(bagSlot, keptFaces[localIndex], bagDice[bagSlot]));
            }
            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Resuelve un único índice local (relativo al subset contribuyente) a su bag slot —
        /// mismo mapeo que <see cref="Resolve"/>/<see cref="ResolveDetailed"/>, factoreado para
        /// consumers que solo necesitan el número de slot (gates carrier-aware como
        /// <c>CarrierParticipates</c> y <c>PcCarrierFace</c>, que no arman <see cref="ContributingDie"/>
        /// completos). Sin mapa (null), <paramref name="localIndex"/> ya se asume bag slot directo
        /// — ver <c>EffectContext.KeptDiceOriginalIndices</c>.
        /// </summary>
        public static int ResolveBagSlot(int localIndex, IReadOnlyList<int> keptDiceOriginalIndices)
        {
            return keptDiceOriginalIndices != null
                   && localIndex >= 0 && localIndex < keptDiceOriginalIndices.Count
                ? keptDiceOriginalIndices[localIndex]
                : localIndex;
        }

        /// <summary>
        /// Conveniencia para efectos/announcers que parten de un <see cref="EffectContext"/>:
        /// resuelve el detalle (slot + cara + tipo) leyendo el bag del enchantment service y
        /// las caras de <c>KeptDice ?? DiceResult</c> (mismo espacio de índices locales que
        /// <paramref name="contributingIndices"/>). Null si no hay bag disponible.
        /// </summary>
        public static IReadOnlyList<ContributingDie> ResolveFromContext(
            EffectContext context, IReadOnlyList<int> contributingIndices)
        {
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants)
                || enchants?.Bag == null)
                return null;
            return ResolveDetailed(
                contributingIndices, context?.KeptDiceOriginalIndices,
                context?.KeptDice ?? context?.DiceResult, enchants.Bag.Dice);
        }

        /// <summary>
        /// Tipos de dado alineados 1:1 con el subset holdeado (caso identidad de
        /// <see cref="Resolve"/>). Usado por los call sites de detección para pasar los tipos
        /// a los overloads tipados de <c>BaseComboSO</c> (Fuerza Bruta necesita el rango de
        /// cada dado ANTES de matchear, no después).
        /// </summary>
        /// <param name="keptDiceOriginalIndices">Mapeo subset→bag slot. Null ⇒ identidad
        /// (el subset ya está en orden de slot).</param>
        /// <param name="bagDice">Tipos de dado del bag, en orden de slot.</param>
        /// <param name="keptCount">Cantidad de dados del subset (define el largo del resultado
        /// cuando el mapeo es identidad).</param>
        public static IReadOnlyList<DiceType> ResolveKept(
            IReadOnlyList<int> keptDiceOriginalIndices,
            IReadOnlyList<DiceType> bagDice,
            int keptCount)
        {
            if (bagDice == null || keptCount <= 0) return null;

            var result = new List<DiceType>(keptCount);
            for (int i = 0; i < keptCount; i++)
            {
                int bagSlot = (keptDiceOriginalIndices != null && i < keptDiceOriginalIndices.Count)
                    ? keptDiceOriginalIndices[i]
                    : i;
                if (bagSlot < 0 || bagSlot >= bagDice.Count) return null;
                result.Add(bagDice[bagSlot]);
            }
            return result;
        }
    }
}
