namespace Rollgeon.Combat.Random
{
    /// <summary>
    /// Envoltorio default sobre <see cref="System.Random"/>. Permite construir
    /// con seed para tests deterministas o sin seed para producción.
    /// </summary>
    /// <remarks>
    /// TODO [Run determinism] — cuando exista <c>RunProgress</c> (§15), pasar
    /// su seed acá para que el mismo run determinístico produzca los mismos
    /// orderings entre sesiones.
    /// </remarks>
    public sealed class DefaultInitiativeRng : IInitiativeRng
    {
        private readonly System.Random _random;

        public DefaultInitiativeRng()
        {
            _random = new System.Random();
        }

        public DefaultInitiativeRng(int seed)
        {
            _random = new System.Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }
    }
}
