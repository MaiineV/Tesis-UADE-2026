namespace Rollgeon.Tiles
{
    /// <summary>
    /// Catálogo de Casillas Especiales (GDD, sección 4). Identifica el "qué es" de la
    /// casilla para reglas cruzadas (Zona de Seguridad declara de qué tipos protege,
    /// el pathing IA distingue Hielo/Portal). La mecánica concreta vive en la
    /// <see cref="SpecialTileDefinitionSO"/>, no acá.
    /// </summary>
    public enum SpecialTileType
    {
        Spikes = 0,
        Fire = 1,
        FireTemp = 2,
        Ice = 3,
        Portal = 4,
        Poison = 5,
        Heal = 6,
        Strength = 7,
        Boost = 8,
        Telegraph = 9,
        ElectricPuddle = 10,
        SafeZone = 11,
    }
}
