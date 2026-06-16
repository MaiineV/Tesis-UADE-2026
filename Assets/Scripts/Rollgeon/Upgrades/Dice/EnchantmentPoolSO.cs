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
            if (Entries == null || Entries.Count == 0) return null;

            EnchantmentSO picked = TryRollFiltered(rng, targetType, floorDepth, exclude);
            if (picked != null) return picked;

            // Fallback: ignorar el exclude por si todos los compatibles ya están aplicados.
            return TryRollFiltered(rng, targetType, floorDepth, exclude: null);
        }

        private EnchantmentSO TryRollFiltered(
            System.Random rng,
            DiceType targetType,
            int floorDepth,
            IReadOnlyCollection<EnchantmentSO> exclude)
        {
            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsEligible(Entries[i], targetType, floorDepth, exclude)) continue;
                total += Entries[i].Weight;
            }
            if (total <= 0f) return null;

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsEligible(Entries[i], targetType, floorDepth, exclude)) continue;
                cursor += Entries[i].Weight;
                if (pick <= cursor) return Entries[i].Enchantment;
            }

            // Floating point drift — fallback al último eligible.
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (IsEligible(Entries[i], targetType, floorDepth, exclude))
                    return Entries[i].Enchantment;
            }
            return null;
        }

        private static bool IsEligible(
            WeightedEnchantment entry,
            DiceType targetType,
            int floorDepth,
            IReadOnlyCollection<EnchantmentSO> exclude)
        {
            if (entry == null || entry.Enchantment == null) return false;
            if (entry.Weight <= 0f) return false;
            if (entry.MinFloorDepth > floorDepth) return false;
            if (!entry.Enchantment.IsCompatibleWith(targetType)) return false;
            if (exclude != null && exclude.Contains(entry.Enchantment)) return false;
            // Meta-progresión (#164): encantamientos gateados quedan fuera hasta desbloquearse.
            if (!MetaUnlockGate.IsAvailable(UnlockableCategory.Enchantment, entry.Enchantment.UpgradeId)) return false;
            return true;
        }
    }
}
