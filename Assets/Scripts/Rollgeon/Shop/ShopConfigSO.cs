using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Shop
{
    /// <summary>
    /// Tuning global de shops — pricing, cantidad de slots, restock.
    /// TECHNICAL.md §17.F.3. Referenciado en el <c>RulesetSO</c> (§14.7) cuando
    /// aterrice, o inyectado directo en el <c>ShopManagerBootstrap</c> por ahora.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Autoridad de pricing.</b> <c>BasePrice</c> lo manda el pool;
    /// <see cref="PriceMultiplier"/> y <see cref="PriceVariance"/> son globals
    /// del ruleset. Fórmula: <c>finalPrice = basePrice * mult * rng(1-var, 1+var)</c>.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Shop/Shop Config",
        fileName = "ShopConfig")]
    public sealed class ShopConfigSO : ScriptableObject
    {
        [Title("Pricing")]
        [InfoBox("Multiplicador global sobre el BasePrice del pool. Hardcore x1.5, arcade x1.0.")]
        [MinValue(0.1f)] public float PriceMultiplier = 1.0f;

        [InfoBox("Varianza aleatoria: finalPrice = basePrice * mult * rng(1-var, 1+var).")]
        [Range(0f, 0.5f)] public float PriceVariance = 0.1f;

        [Title("Slots")]
        [InfoBox("Cantidad máxima de spawn points de shop items que el service usa. " +
                 "Si el prefab tiene más RewardSpawnPoints, se usan los primeros N.")]
        [MinValue(1)] public int MaxItemSlots = 4;

        [Title("Restock (MVP: no wired — follow-up)")]
        [ToggleLeft] public bool AllowRestock = false;

        [ShowIf(nameof(AllowRestock))]
        [MinValue(0)] public int RestockCost = 5;

        [ShowIf(nameof(AllowRestock))]
        [MinValue(0)] public int MaxRestocks = 1;

        [Title("Discount (MVP: no wired — follow-up)")]
        [InfoBox("First-purchase discount por shop room — se aplica via HasHadFirstPurchase flag. No wired en el MVP.")]
        [Range(0, 100)] public int FirstPurchaseDiscountPercent = 0;

        [Title("Prefab")]
        [Required, Tooltip("Prefab instanciado por cada slot no comprado. Debe tener un ShopItemPedestalInteractable.")]
        public GameObject PedestalPrefab;

        /// <summary>Resuelve el precio final con multiplicador y varianza.</summary>
        public int ResolvePrice(int basePrice, System.Random rng)
        {
            if (basePrice <= 0) return 0;
            float variance = Mathf.Clamp(PriceVariance, 0f, 0.5f);
            float rngFactor = variance <= 0f
                ? 1f
                : 1f - variance + (float)rng.NextDouble() * (variance * 2f);
            float raw = basePrice * Mathf.Max(0.1f, PriceMultiplier) * rngFactor;
            return Mathf.Max(1, Mathf.RoundToInt(raw));
        }
    }
}
