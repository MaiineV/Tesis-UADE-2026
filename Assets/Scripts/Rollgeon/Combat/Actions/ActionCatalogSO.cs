using System.Collections.Generic;
using System.Linq;
using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Combat.Actions
{
    /// <summary>
    /// Catalogo maestro unificado de <see cref="ActionDefinitionSO"/> — UNA fuente de verdad
    /// para "cosa que un actor puede ejecutar en combate". TECHNICAL.md §12.6.0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hereda toda la infra de <see cref="BaseCatalogSO{T}"/> (Foundation#0005): validators
    /// de duplicados / nulls, <see cref="BaseCatalogSO{T}.AllIds"/>,
    /// <see cref="BaseCatalogSO{T}.GetById"/>, <see cref="BaseCatalogSO{T}.Contains"/>. Este
    /// tipo solo agrega el lookup por <see cref="ActionType"/> y el cast seguro del
    /// <see cref="ActionDefinitionSO.BackingAsset"/>.
    /// </para>
    /// <para>
    /// <b>Registro.</b> Se agrega a <c>ServiceBootstrapSO.Catalogs</c> y se publica en el
    /// <c>ServiceLocator</c> bajo <see cref="ActionCatalogSO"/> (typeof runtime) durante el
    /// bootstrap global.
    /// </para>
    /// </remarks>
    // [SETUP] El usuario crea el .asset via Create menu (Assets/ResourcesData/Catalogs/ActionCatalog.asset),
    // lo puebla con los ActionDefinitionSO del FP, y lo agrega al ServiceBootstrapSO.Catalogs.
    // Ver docs/setup/System#0100b_ActionEconomyRepetition.md.
    [CreateAssetMenu(menuName = "Rollgeon/Actions/Action Catalog", fileName = "ActionCatalog")]
    public sealed class ActionCatalogSO : BaseCatalogSO<ActionDefinitionSO>
    {
        /// <inheritdoc />
        protected override string GetIdOf(ActionDefinitionSO entry)
            => entry != null ? entry.ActionId : null;

        /// <summary>
        /// Lista de <see cref="ActionDefinitionSO.ActionId"/> filtrada por
        /// <paramref name="type"/>. Orden = orden de insercion en <c>_entries</c>.
        /// </summary>
        public IEnumerable<string> GetIdsByType(ActionType type)
        {
            return Entries
                .Where(e => e != null && e.Type == type)
                .Select(e => e.ActionId);
        }

        /// <summary>
        /// Cast seguro del <see cref="ActionDefinitionSO.BackingAsset"/> al tipo
        /// <typeparamref name="T"/>. Devuelve <c>null</c> si el id no existe, si el
        /// BackingAsset es null, o si el type no matchea.
        /// </summary>
        /// <example>
        /// <code>
        /// var combo = catalog.GetBackingAsset&lt;BaseComboSO&gt;("combo.full_house");
        /// </code>
        /// </example>
        public T GetBackingAsset<T>(string id) where T : ScriptableObject
        {
            var entry = GetById(id);
            return entry != null ? entry.BackingAsset as T : null;
        }
    }
}
