using Rollgeon.Patterns.Catalogs;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Catalogo polimorfico de <see cref="EnemyDataSO"/>. Alimenta el dropdown del
    /// <c>EntityId</c> + spawn de combat. TECHNICAL.md §1.1.1 / §7.1.
    /// </summary>
    /// <remarks>
    /// <b>Registro</b>: agregar la instancia del asset a <c>ServiceBootstrapSO.Catalogs</c>
    /// para que BaseCatalogSO se registre en el <c>ServiceLocator</c> durante la
    /// escena <c>00_Bootstrap</c>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Catalogs/Enemy Catalog", fileName = "EnemyCatalog")]
    public class EnemyCatalogSO : BaseCatalogSO<EnemyDataSO>
    {
        protected override string GetIdOf(EnemyDataSO entry) => entry != null ? entry.EntityId : null;
    }
}
