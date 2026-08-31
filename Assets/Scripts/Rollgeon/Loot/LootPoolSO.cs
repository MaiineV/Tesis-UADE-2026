using System.Collections.Generic;
using Rollgeon.Items;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Loot
{
    /// <summary>
    /// Pesos por categoría de rareza de un <see cref="LootPoolSO"/>. Relativos, no
    /// necesitan sumar 100 — espejo de <c>ChestFloorTierWeights</c> sin el floor
    /// (esa clase vive Odin-serializada dentro de ChestConfig.asset y tocarla es
    /// riesgo de migración).
    /// </summary>
    [System.Serializable, HideReferenceObjectPicker]
    public class RarityWeights
    {
        [MinValue(0f)] public float Common = 1f;
        [MinValue(0f)] public float Uncommon = 1f;
        [MinValue(0f)] public float Rare = 1f;
        [MinValue(0f)] public float Legendary = 1f;

        // Dios (item-editor-spec.md §5.1): default 1f, igual que sus hermanos —
        // uniforme salvo que Balance lo ajuste. Field nuevo en clase ya serializada:
        // Odin lo completa con este default en los pools existentes sin migración.
        [MinValue(0f)] public float God = 1f;

        /// <summary>
        /// Peso de la categoría. Switch exhaustivo A PROPÓSITO: antes de agregar
        /// <see cref="ItemRarity.God"/> el <c>default:</c> devolvía <see cref="Common"/>
        /// para cualquier valor no listado — un ítem Dios en un pool con Common en 0
        /// pesaba como si el pool lo excluyera a él y no a Common. Ahora cada caso es
        /// explícito.
        /// </summary>
        public float WeightFor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Common: return Common;
                case ItemRarity.Uncommon: return Uncommon;
                case ItemRarity.Rare: return Rare;
                case ItemRarity.Legendary: return Legendary;
                case ItemRarity.God: return God;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(rarity), rarity, "ItemRarity sin peso definido en RarityWeights.");
            }
        }
    }

    /// <summary>
    /// Pool de ítems genérico y reutilizable (cofres hoy; salas, spawns, etc. a
    /// futuro): una lista editable de QUÉ objetos puede integrar + porcentaje por
    /// categoría de rareza. El roll elige primero la categoría por peso (solo entre
    /// categorías que tienen ítems en la lista) y después un ítem uniforme dentro
    /// de ella — la rareza de cada ítem es la suya propia (<see cref="ItemSO.Rarity"/>),
    /// el pool no la redefine.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Loot/Loot Pool", fileName = "LootPool")]
    public sealed class LootPoolSO : SerializedScriptableObject
    {
        [Title("Ítems")]
        [InfoBox("Qué objetos puede entregar este pool. La categoría de cada ítem es su " +
                 "propia Rarity; los pesos de abajo definen cuán probable es cada categoría.")]
        [ListDrawerSettings(ShowFoldout = false)]
        [OdinSerialize]
        public List<ItemSO> Items = new List<ItemSO>();

        [Title("Pesos por categoría")]
        [InfoBox("Relativos (no hace falta que sumen 100). Peso 0 = la categoría nunca " +
                 "sale. Una categoría sin ítems en la lista se ignora y el resto renormaliza.")]
        public RarityWeights Weights = new RarityWeights();

        /// <summary>
        /// Rolea un ítem: categoría por peso acumulado, ítem uniforme dentro.
        /// Pool vacío ⇒ <c>null</c> (el consumidor degrada — ej. el cofre da oro).
        /// Total de pesos 0 sobre las categorías pobladas ⇒ uniforme entre todos.
        /// </summary>
        public ItemSO RollItem(System.Random rng)
        {
            var byRarity = GroupValidByRarity();
            if (byRarity.Count == 0) return null;

            float total = 0f;
            foreach (var pair in byRarity) total += Weights.WeightFor(pair.Key);

            if (total <= 0f) return UniformOverAll(byRarity, rng);

            float pick = (float)rng.NextDouble() * total;
            float cursor = 0f;
            foreach (var pair in byRarity)
            {
                cursor += Weights.WeightFor(pair.Key);
                if (pick <= cursor)
                    return pair.Value[rng.Next(pair.Value.Count)];
            }

            // Floating point drift — fallback a la última categoría poblada CON peso
            // (una de peso 0 jamás debe salir, ni siquiera por drift).
            for (int i = byRarity.Count - 1; i >= 0; i--)
            {
                if (Weights.WeightFor(byRarity[i].Key) <= 0f) continue;
                var bucket = byRarity[i].Value;
                return bucket[rng.Next(bucket.Count)];
            }
            return null;
        }

        /// <summary>
        /// Ítems válidos del pool, en orden autorado — para previews (ej. el relleno
        /// del reel gacha). Nunca <c>null</c>; puede ser vacío.
        /// </summary>
        public IReadOnlyList<ItemSO> GetPreview()
        {
            var preview = new List<ItemSO>(Items != null ? Items.Count : 0);
            if (Items == null) return preview;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] != null) preview.Add(Items[i]);
            }
            return preview;
        }

        // Lista (no dict) para orden de iteración determinista: el orden de
        // categorías sigue ItemRarity ascendente, siempre.
        private List<KeyValuePair<ItemRarity, List<ItemSO>>> GroupValidByRarity()
        {
            var result = new List<KeyValuePair<ItemRarity, List<ItemSO>>>(5);
            if (Items == null) return result;

            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                List<ItemSO> bucket = null;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Items[i] == null || Items[i].Rarity != rarity) continue;
                    bucket ??= new List<ItemSO>();
                    bucket.Add(Items[i]);
                }
                if (bucket != null)
                    result.Add(new KeyValuePair<ItemRarity, List<ItemSO>>(rarity, bucket));
            }
            return result;
        }

        private static ItemSO UniformOverAll(
            List<KeyValuePair<ItemRarity, List<ItemSO>>> byRarity, System.Random rng)
        {
            int count = 0;
            foreach (var pair in byRarity) count += pair.Value.Count;
            int slot = rng.Next(count);
            foreach (var pair in byRarity)
            {
                if (slot < pair.Value.Count) return pair.Value[slot];
                slot -= pair.Value.Count;
            }
            return null;
        }
    }
}
