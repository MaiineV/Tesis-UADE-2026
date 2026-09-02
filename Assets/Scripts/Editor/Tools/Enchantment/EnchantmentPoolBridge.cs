using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Membresía y peso de un encantamiento en el <see cref="EnchantmentPoolSO"/> del
    /// altar — el análogo de <c>ItemShopPriceBridge</c> con el eje económico cambiado:
    /// acá no hay precio por asset (el costo es global en <c>EnchantmentConfigSO</c>),
    /// el dial de balance es el <b>peso de aparición</b> y el <c>MinFloorDepth</c>.
    /// </summary>
    /// <remarks>
    /// <c>Entries</c> es <c>[OdinSerialize]</c>: invisible a <c>SerializedProperty</c>,
    /// así que toda mutación va Record → mutar → Dirty sobre el objeto (item-editor-spec
    /// §7 regla 1). Sin entrada en el pool, el encantamiento no se ofrece nunca;
    /// <c>Weight = 0</c> lo deshabilita sin borrar la entry.
    /// </remarks>
    public static class EnchantmentPoolBridge
    {
        /// <summary>
        /// El pool del altar. Único en el proyecto (wireado en
        /// <c>EnchantmentRoomBootstrap.asset</c>) — se localiza por tipo, no por path,
        /// para sobrevivir un move.
        /// </summary>
        public static EnchantmentPoolSO LoadDefaultPool()
        {
            var guids = AssetDatabase.FindAssets("t:" + nameof(EnchantmentPoolSO));
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<EnchantmentPoolSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static bool IsInPool(EnchantmentPoolSO pool, EnchantmentSO enchantment)
            => TryFindIndex(pool, enchantment, out _);

        /// <summary>Peso autorado de la entry. <c>false</c> si el encantamiento no está en el pool.</summary>
        public static bool TryGetWeight(EnchantmentPoolSO pool, EnchantmentSO enchantment, out float weight)
        {
            weight = 0f;
            if (!TryFindIndex(pool, enchantment, out int index)) return false;
            weight = pool.Entries[index].Weight;
            return true;
        }

        /// <summary><c>MinFloorDepth</c> de la entry. <c>false</c> si no está en el pool.</summary>
        public static bool TryGetMinFloorDepth(EnchantmentPoolSO pool, EnchantmentSO enchantment, out int minFloorDepth)
        {
            minFloorDepth = 0;
            if (!TryFindIndex(pool, enchantment, out int index)) return false;
            minFloorDepth = pool.Entries[index].MinFloorDepth;
            return true;
        }

        /// <summary>Cambia el peso de una entry existente. <c>false</c> si no está en el pool.</summary>
        public static bool SetWeight(EnchantmentPoolSO pool, EnchantmentSO enchantment, float weight)
        {
            if (weight < 0f || !TryFindIndex(pool, enchantment, out int index)) return false;

            Undo.RecordObject(pool, "Set Enchantment Weight");
            pool.Entries[index].Weight = weight;
            EditorUtility.SetDirty(pool);
            return true;
        }

        /// <summary>Cambia el piso mínimo de una entry existente. <c>false</c> si no está en el pool.</summary>
        public static bool SetMinFloorDepth(EnchantmentPoolSO pool, EnchantmentSO enchantment, int minFloorDepth)
        {
            if (minFloorDepth < 0 || !TryFindIndex(pool, enchantment, out int index)) return false;

            Undo.RecordObject(pool, "Set Enchantment Min Floor");
            pool.Entries[index].MinFloorDepth = minFloorDepth;
            EditorUtility.SetDirty(pool);
            return true;
        }

        /// <summary>
        /// Agrega la entry al pool. <c>false</c> si ya estaba (usar los setters para
        /// ajustar una existente) o los valores son inválidos.
        /// </summary>
        public static bool AddToPool(
            EnchantmentPoolSO pool, EnchantmentSO enchantment, float weight = 1f, int minFloorDepth = 0)
        {
            if (pool == null || enchantment == null) return false;
            if (weight < 0f || minFloorDepth < 0) return false;
            if (IsInPool(pool, enchantment)) return false;

            Undo.RecordObject(pool, "Add Enchantment To Pool");
            pool.Entries.Add(new WeightedEnchantment
            {
                Enchantment = enchantment,
                Weight = weight,
                MinFloorDepth = minFloorDepth,
            });
            EditorUtility.SetDirty(pool);
            return true;
        }

        /// <summary>Saca la entry del pool. <c>false</c> si no estaba.</summary>
        public static bool RemoveFromPool(EnchantmentPoolSO pool, EnchantmentSO enchantment)
        {
            if (!TryFindIndex(pool, enchantment, out int index)) return false;

            Undo.RecordObject(pool, "Remove Enchantment From Pool");
            pool.Entries.RemoveAt(index);
            EditorUtility.SetDirty(pool);
            return true;
        }

        static bool TryFindIndex(EnchantmentPoolSO pool, EnchantmentSO enchantment, out int index)
        {
            index = -1;
            if (pool == null || pool.Entries == null || enchantment == null) return false;

            for (int i = 0; i < pool.Entries.Count; i++)
            {
                if (pool.Entries[i] != null && pool.Entries[i].Enchantment == enchantment)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }
    }
}
