using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rollgeon.Patterns.Catalogs
{
    /// <summary>
    /// Shape no-generico de un catalogo. Permite que <c>ServiceBootstrapSO</c> itere
    /// sobre una lista polimorfica (<c>List&lt;BaseCatalogSO&gt;</c>) sin conocer el
    /// <c>T</c> concreto de cada subclase. Definido en el plan §4.1.
    /// <para>
    /// La convencion <b>"IDs como dropdowns"</b> (TECHNICAL.md §0) se cumple porque
    /// toda herramienta que popule un <c>[ValueDropdown]</c> resuelve el catalogo via
    /// <see cref="ServiceLocator"/> y consume <see cref="AllIds"/>.
    /// </para>
    /// </summary>
    public interface ICatalog
    {
        /// <summary>
        /// Nombre humano del catalogo (default: <c>GetType().Name</c>). Util para logs
        /// del <c>BootstrapRunner</c> y para inspectores que muestran una lista de catalogos
        /// registrados.
        /// </summary>
        string CatalogName { get; }

        /// <summary>
        /// Enumera los IDs (strings) de todos los entries no-null del catalogo.
        /// Pensado para alimentar <c>[ValueDropdown]</c> de Odin. El orden es el
        /// del listado interno del catalogo (sin ordenamiento implicito).
        /// </summary>
        IEnumerable<string> AllIds { get; }

        /// <summary>
        /// Hook de pre-load asincrono. Default: no-op (<see cref="Task.CompletedTask"/>).
        /// Subclases concretas como <c>EntityCatalogSO</c> (downstream) lo sobreescriben
        /// para disparar carga Addressables antes del <c>LoadScene(MainMenu)</c>.
        /// </summary>
        Task PreloadAsync();
    }
}
