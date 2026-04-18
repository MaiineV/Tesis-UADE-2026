namespace Rollgeon.Patterns.Bootstrap
{
    /// <summary>
    /// Marker interface para servicios runtime pre-instanciados que se enganchan al
    /// bootstrap global. Plan §4.3, TECHNICAL.md §1.1.1.
    /// <para>
    /// <b>Semantica.</b> <see cref="Register"/> es invocado por
    /// <c>ServiceBootstrapSO.RegisterAll()</c> durante <c>BootstrapRunner.Awake</c>.
    /// La implementacion concreta es responsable de hacer su propio
    /// <c>ServiceLocator.AddService&lt;ISelfType&gt;(this, ServiceScope.Global)</c>
    /// (o cualquier wiring interno que requiera).
    /// </para>
    /// <para>
    /// <b>Priority.</b> Determina el orden de invocacion de <see cref="Register"/>
    /// cuando hay multiples <see cref="IPreloadableService"/> en la lista
    /// <c>ExtraServices</c>. Menor = antes. Default 0 (orden indefinido entre pares).
    /// </para>
    /// </summary>
    public interface IPreloadableService
    {
        /// <summary>
        /// Registra este servicio en el <c>ServiceLocator</c> y prepara cualquier
        /// estado inicial que deba vivir durante toda la sesion.
        /// </summary>
        void Register();

        /// <summary>
        /// Prioridad de registro entre ExtraServices. Menor = antes.
        /// </summary>
        int Priority => 0;
    }
}
