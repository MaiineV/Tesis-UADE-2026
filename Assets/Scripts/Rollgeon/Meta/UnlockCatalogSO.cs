using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Meta
{
    /// <summary>
    /// Catálogo de <see cref="UnlockDefinitionSO"/> (#164). Se agrega a la lista
    /// <c>Catalogs</c> del <c>ServiceBootstrapSO</c> para que el
    /// <c>MetaProgressionService</c> lo resuelva al registrarse y los pools queden
    /// gateados antes de que el jugador toque ningún menú.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Meta/Unlock Catalog", fileName = "UnlockCatalog")]
    public class UnlockCatalogSO : BaseCatalogSO<UnlockDefinitionSO>
    {
        protected override string GetIdOf(UnlockDefinitionSO entry) => entry != null ? entry.UnlockId : null;

        /// <summary>
        /// Agrega una entry si no estaba (<c>_entries</c> es protected en la base).
        /// Usado por tools de editor (setup #164) y EditMode tests.
        /// </summary>
        public void AddEntry(UnlockDefinitionSO entry)
        {
            if (entry != null && !_entries.Contains(entry)) _entries.Add(entry);
        }
    }
}
