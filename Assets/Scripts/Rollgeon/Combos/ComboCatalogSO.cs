using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Combos
{
    /// <summary>
    /// Catalogo central de combos. Alimenta el <c>[ValueDropdown]</c> transversal (§0) y el
    /// lookup por <see cref="BaseComboSO.ComboId"/>. Usa toda la infra de
    /// <see cref="BaseCatalogSO{T}"/> (Foundation#0005): validadores de duplicados, nulls,
    /// <c>AllIds</c>, <c>GetById</c>, <c>Contains</c>.
    /// </summary>
    // [SETUP] El usuario crea el .asset via Create menu (Assets/Rollgeon/Combos/ComboCatalog.asset),
    // lo puebla con los 8 .asset de combos, y lo agrega al ServiceBootstrapSO.
    // Ver docs/setup/Content#0097a_ComboBaseAndConcretes.md.
    [CreateAssetMenu(menuName = "Rollgeon/Catalogs/Combo Catalog", fileName = "ComboCatalog")]
    public class ComboCatalogSO : BaseCatalogSO<BaseComboSO>
    {
        /// <inheritdoc />
        protected override string GetIdOf(BaseComboSO entry)
            => entry != null ? entry.ComboId : null;
    }
}
