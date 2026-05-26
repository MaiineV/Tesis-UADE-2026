using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Config del altar de la Sala de Encantamiento — costos y reglas balance.
    /// Mismo patrón que <c>ShopConfigSO</c>: un .asset por config canónica,
    /// referenciado desde el bootstrap del service.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Dice/Enchantment Config",
        fileName = "EnchantmentConfig")]
    public sealed class EnchantmentConfigSO : ScriptableObject
    {
        [Title("Cost")]
        [MinValue(0)]
        [Tooltip("Oro que cuesta cada uso del altar. El GDD dice que el límite es " +
                 "puramente económico — no hay tope de usos mientras alcance el oro.")]
        [SerializeField]
        private int _baseCost = 15;

        [MinValue(1f)]
        [Tooltip("Multiplicador compuesto por cada re-roll consecutivo del MISMO slot. " +
                 "Ej. 1.5 con costo base 15 → 1° encanto: 15G, 1° re-roll: 23G, 2° re-roll: 34G, " +
                 "3° re-roll: 51G, ... El contador es per-(dado, slot): cambiar de slot/dado " +
                 "resetea desde 0 para el nuevo slot. 1.0 = sin escalado (todos los rolls cuestan base).")]
        [SerializeField]
        private float _reEnchantCostMultiplier = 1f;

        [Title("Validation")]
        [Tooltip("Si está activo, el sistema rechaza encantamientos cuya intersección " +
                 "con los ya aplicados dejaría al dado con menos caras que este threshold. " +
                 "1 = solo bloquea pool vacío. >1 = bloquea también estados extremos.")]
        [MinValue(1)]
        [SerializeField]
        private int _minFacesAfterApply = 1;

        /// <summary>Costo base por uso del altar.</summary>
        public int BaseCost => _baseCost;

        /// <summary>Multiplicador aplicado cuando se re-encanta un slot ya ocupado.</summary>
        public float ReEnchantCostMultiplier => _reEnchantCostMultiplier;

        /// <summary>
        /// Resuelve el costo del próximo uso del altar sobre un slot que ya fue
        /// re-rolleado <paramref name="rerollCount"/> veces. Fórmula:
        /// <c>base × multiplier ^ rerollCount</c> (compuesto).
        /// </summary>
        /// <param name="rerollCount">
        /// Veces que se encantó este slot previamente. 0 = primer encanto (cuesta base);
        /// 1 = primer re-roll; 2 = segundo re-roll; etc.
        /// </param>
        public int ResolveCost(int rerollCount)
        {
            if (rerollCount <= 0 || _reEnchantCostMultiplier <= 1f) return _baseCost;
            float scaled = _baseCost * Mathf.Pow(_reEnchantCostMultiplier, rerollCount);
            return Mathf.CeilToInt(scaled);
        }

        /// <summary>
        /// Mínimo de caras válidas que debe tener un dado tras aplicar el encantamiento.
        /// La validación bloquea el apply si quedaría por debajo de este threshold.
        /// </summary>
        public int MinFacesAfterApply => _minFacesAfterApply;
    }
}
