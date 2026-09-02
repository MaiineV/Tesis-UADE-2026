using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Dice;
using Rollgeon.Meta;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Pool pesado de encantamientos elegibles para un floor / tema. El altar de
    /// la Sala de Encantamiento rolea contra este pool al confirmar la mejora.
    /// Mismo patrón que <c>ShopPoolSO</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Doble filtro al rolear.</b> El pool filtra por <c>floorDepth</c>
    /// (entries con <c>MinFloorDepth &gt; depth</c> se saltean) y por
    /// <c>EnchantmentSO.IsCompatibleWith(diceType)</c> (encantamientos no
    /// compatibles con el dado target se saltean). El service también puede
    /// inyectar más filtros (ej. validación de intersección vacía) sin tocar
    /// este SO.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Dice/Enchantment Pool",
        fileName = "EnchantmentPool")]
    public sealed class EnchantmentPoolSO : SerializedScriptableObject
    {
        [Title("Entries")]
        [InfoBox("Pool pesado. Un entry se rolea cada vez que el jugador usa el altar. " +
                 "Pesos son relativos. Entries con Weight = 0 se saltean (útil para deshabilitar " +
                 "sin borrar la entry).")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<WeightedEnchantment> Entries = new List<WeightedEnchantment>();

        /// <summary>
        /// Rolea un encantamiento compatible con <paramref name="targetType"/>.
        /// Devuelve <c>null</c> si no hay entries elegibles tras los filtros.
        /// </summary>
        /// <param name="rng">RNG inyectable para tests determinísticos.</param>
        /// <param name="targetType">Tipo del dado que va a recibir el encantamiento.</param>
        /// <param name="floorDepth">Profundidad del floor actual (para filtrar entries con MinFloorDepth).</param>
        /// <param name="exclude">
        /// Encantamientos ya activos en el dado — el pool intenta no devolverlos
        /// (re-encantar con el mismo encantamiento no es útil). Si todos los
        /// compatibles están excluidos, los considera de nuevo como fallback.
        /// </param>
        public EnchantmentSO Roll(
            System.Random rng,
            DiceType targetType,
            int floorDepth,
            IReadOnlyCollection<EnchantmentSO> exclude = null)
        {
            return Roll(rng, new[] { targetType }, floorDepth, exclude);
        }

        /// <summary>
        /// Variante multi-dado (slot machine con palanca-primero): un entry es
        /// elegible si es compatible con AL MENOS UNO de los tipos del bag —
        /// el dado destino se elige después de ver la oferta.
        /// </summary>
        public EnchantmentSO Roll(
            System.Random rng,
            IReadOnlyList<DiceType> targetTypes,
            int floorDepth,
            IReadOnlyCollection<EnchantmentSO> exclude = null)
        {
            return Roll(rng, targetTypes, floorDepth, exclude, filter: null);
        }

        /// <summary>
        /// Variante con predicado extra sobre el encantamiento (ej. "solo malditos" para el
        /// slot garantizado de Moneda Maldita). El filtro se aplica en las dos pasadas —
        /// incluida la que ignora el exclude — así un roll "solo malditos" nunca devuelve
        /// un bendecido por fallback.
        /// </summary>
        public EnchantmentSO Roll(
            System.Random rng,
            IReadOnlyList<DiceType> targetTypes,
            int floorDepth,
            IReadOnlyCollection<EnchantmentSO> exclude,
            Func<EnchantmentSO, bool> filter)
        {
            if (Entries == null || Entries.Count == 0) return null;
            if (targetTypes == null || targetTypes.Count == 0) return null;

            EnchantmentSO picked = TryRollFiltered(rng, targetTypes, floorDepth, exclude, filter);
            if (picked != null) return picked;

            // Fallback: ignorar el exclude por si todos los compatibles ya están aplicados.
            return TryRollFiltered(rng, targetTypes, floorDepth, exclude: null, filter);
        }

        private EnchantmentSO TryRollFiltered(
            System.Random rng,
            IReadOnlyList<DiceType> targetTypes,
            int floorDepth,
            IReadOnlyCollection<EnchantmentSO> exclude,
            Func<EnchantmentSO, bool> filter)
        {
            // Resuelto UNA vez por roll y aplicado idéntico en la acumulación y en el
            // cursor — si difieren, la ruleta queda sesgada respecto del total.
            float cursedMult = ResolveCursedWeightMultiplier();

            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsEligible(Entries[i], targetTypes, floorDepth, exclude, filter)) continue;
                total += EffectiveWeight(Entries[i], cursedMult);
            }
            if (total <= 0f) return null;

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsEligible(Entries[i], targetTypes, floorDepth, exclude, filter)) continue;
                cursor += EffectiveWeight(Entries[i], cursedMult);
                if (pick <= cursor) return Entries[i].Enchantment;
            }

            // Floating point drift — fallback al último eligible.
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (IsEligible(Entries[i], targetTypes, floorDepth, exclude, filter))
                    return Entries[i].Enchantment;
            }
            return null;
        }

        /// <summary>
        /// Peso efectivo de la entry: el autorado, escalado por el multiplicador de
        /// malditos cuando el encantamiento lo es (Moneda Maldita). El check de
        /// elegibilidad (<c>Weight &lt;= 0</c>) sigue sobre el peso crudo.
        /// </summary>
        private static float EffectiveWeight(WeightedEnchantment entry, float cursedMult)
        {
            return IsCursedForPool(entry.Enchantment) ? entry.Weight * cursedMult : entry.Weight;
        }

        /// <summary>
        /// "Maldito" a efectos del pool: <see cref="EnchantmentCapabilityQueries.IsCursed"/>
        /// o categoría Caos. Es el mismo criterio que escala el peso con Moneda Maldita y
        /// el que usa el slot garantizado del altar — si difieren, el item promete una cosa
        /// y la ruleta entrega otra.
        /// </summary>
        public static bool IsCursedForPool(EnchantmentSO ench)
        {
            if (ench == null) return false;
            // Caos = taxonomía GDD vigente; Maldicion queda como legacy por si un asset
            // viejo no pasó por Assign Categories.
            return ench.IsCursed()
                   || ench.Category == EnchantmentCategory.Caos
                   || ench.Category == EnchantmentCategory.Maldicion;
        }

        /// <summary>
        /// Multiplicador de peso de malditos aportado por items. Degrada a 1 sin
        /// servicio registrado (tests, tooling de editor) — mismo criterio permisivo
        /// que <see cref="Rollgeon.Items.UniquePerRunGate"/>.
        /// </summary>
        private static float ResolveCursedWeightMultiplier()
        {
            return global::Patterns.ServiceLocator.TryGetService<IEnchantmentWeightModifierService>(out var svc)
                   && svc != null
                ? svc.ResolveCursedMultiplier()
                : 1f;
        }

        private static bool IsEligible(
            WeightedEnchantment entry,
            IReadOnlyList<DiceType> targetTypes,
            int floorDepth,
            IReadOnlyCollection<EnchantmentSO> exclude,
            Func<EnchantmentSO, bool> filter)
        {
            if (entry == null || entry.Enchantment == null) return false;
            if (filter != null && !filter(entry.Enchantment)) return false;
            if (entry.Weight <= 0f) return false;
            if (entry.MinFloorDepth > floorDepth) return false;
            if (!IsCompatibleWithAny(entry.Enchantment, targetTypes)) return false;
            if (exclude != null && exclude.Contains(entry.Enchantment)) return false;
            // Meta-progresión (#164): encantamientos gateados quedan fuera hasta desbloquearse.
            if (!MetaUnlockGate.IsAvailable(UnlockableCategory.Enchantment, entry.Enchantment.UpgradeId)) return false;
            return true;
        }

        private static bool IsCompatibleWithAny(EnchantmentSO ench, IReadOnlyList<DiceType> targetTypes)
        {
            for (int i = 0; i < targetTypes.Count; i++)
            {
                if (ench.IsCompatibleWith(targetTypes[i])) return true;
            }
            return false;
        }
    }
}
