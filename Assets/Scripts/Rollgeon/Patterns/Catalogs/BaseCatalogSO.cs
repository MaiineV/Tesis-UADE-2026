using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Patterns.Catalogs
{
    /// <summary>
    /// Base <b>no-generica</b> de todo catalogo. Existe unicamente para permitir que
    /// <c>ServiceBootstrapSO.Catalogs</c> sea una <c>List&lt;BaseCatalogSO&gt;</c> polimorfica
    /// que acepte subclases con distintos <c>T</c>.
    /// <para>
    /// No se crean assets directamente de esta clase: no tiene <c>[CreateAssetMenu]</c>.
    /// Downstream worktrees (T97a ComboCatalog, T99 EntityCatalog, T100b ActionCatalog, etc.)
    /// heredan de <see cref="BaseCatalogSO{T}"/> y definen su propio <c>[CreateAssetMenu]</c>.
    /// </para>
    /// </summary>
    public abstract class BaseCatalogSO : SerializedScriptableObject, ICatalog
    {
        /// <inheritdoc />
        public virtual string CatalogName => GetType().Name;

        /// <inheritdoc />
        public abstract IEnumerable<string> AllIds { get; }

        /// <inheritdoc />
        public virtual Task PreloadAsync() => Task.CompletedTask;
    }

    /// <summary>
    /// SO generico abstracto del que heredan todos los catalogos de datos del juego
    /// (Entity, Combo, Action, Item, Reward, Ruleset, ...). Plan §4.2.
    /// <para>
    /// <b>Contrato de subclase.</b> Cada concrete catalogo:
    /// <list type="number">
    /// <item><description>Define <c>T</c> (un SO concreto o una interfaz).</description></item>
    /// <item><description>Implementa <see cref="GetIdOf"/> apuntando al campo <c>id</c> del entry (e.g. <c>entry.EntityId</c>).</description></item>
    /// <item><description>Agrega <c>[CreateAssetMenu(menuName = "Rollgeon/...")]</c>.</description></item>
    /// </list>
    /// Con eso, el dropdown transversal de §0 funciona automaticamente via <see cref="AllIds"/>.
    /// </para>
    /// <para>
    /// <b>Performance.</b> <c>GetById</c> es <c>O(n)</c> con <c>FirstOrDefault</c>. Aceptable
    /// porque todos los catalogos del proyecto se mantienen por debajo de &lt;100 entries
    /// (hipotesis explicita del plan §4.2). Si un catalogo excede ese rango, migrar a un
    /// <c>Dictionary&lt;string, T&gt;</c> cacheado en <c>OnEnable</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Tipo del entry. Suele ser un ScriptableObject concreto, pero puede ser una interfaz.</typeparam>
    [InfoBox("Los IDs aqui listados alimentan los [ValueDropdown] transversales (TECHNICAL.md §0). " +
             "IDs duplicados rompen la resolucion: validar en el inspector.")]
    public abstract class BaseCatalogSO<T> : BaseCatalogSO
    {
        [Title("Entries")]
        [OdinSerialize]
        [ValidateInput(nameof(ValidateNoDuplicateIds), "IDs duplicados detectados.")]
        [ValidateInput(nameof(ValidateNoNullEntries), "Hay entries null en la lista.")]
        protected List<T> _entries = new List<T>();

        /// <summary>
        /// Extrae el ID string de un entry. Cada subclase decide que campo usar
        /// (p.ej. <c>entry.EntityId</c>, <c>entry.ComboId</c>).
        /// </summary>
        protected abstract string GetIdOf(T entry);

        /// <inheritdoc />
        public override IEnumerable<string> AllIds =>
            _entries
                .Where(e => e != null)
                .Select(GetIdOf)
                .Where(id => !string.IsNullOrEmpty(id));

        /// <summary>
        /// Lista read-only de todos los entries (incluye posibles nulls — el consumer
        /// debe filtrarlos). Expuesto para herramientas de editor y auditorias.
        /// </summary>
        public IReadOnlyList<T> Entries => _entries;

        /// <summary>
        /// Devuelve el entry cuyo id coincide con <paramref name="id"/>, o <c>default(T)</c>
        /// si no existe. Match exacto (case-sensitive).
        /// </summary>
        public T GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return default;
            return _entries.FirstOrDefault(e => e != null && GetIdOf(e) == id);
        }

        /// <summary>
        /// <c>true</c> si existe un entry con ese id. Convenience wrapper sobre <see cref="AllIds"/>.
        /// </summary>
        public bool Contains(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return AllIds.Contains(id);
        }

        // ---- Odin validators (privados, solo para el inspector) -----------------

        private bool ValidateNoDuplicateIds(List<T> entries)
        {
            if (entries == null) return true;
            var seen = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var id = GetIdOf(entry);
                if (string.IsNullOrEmpty(id)) continue;
                if (!seen.Add(id)) return false;
            }
            return true;
        }

        private bool ValidateNoNullEntries(List<T> entries)
        {
            if (entries == null) return true;
            return entries.All(e => e != null);
        }
    }
}
