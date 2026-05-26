namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Tipos de sala canónicos del dungeon. Cualquier entry nuevo se agrega al final
    /// para no shiftear valores int serializados en RoomSO.asset existentes.
    /// </summary>
    public enum RoomType
    {
        Start,
        Combat,
        Boss,
        Shop,
        Potion,
        Enchantment
    }
}
