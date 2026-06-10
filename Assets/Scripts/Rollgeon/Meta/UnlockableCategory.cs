namespace Rollgeon.Meta
{
    /// <summary>
    /// Las cinco categorías de desbloqueables de la meta-progresión (#164).
    /// El <c>TargetId</c> de un <see cref="UnlockDefinitionSO"/> se interpreta
    /// según la categoría: nombre del <see cref="Rollgeon.Dice.DiceType"/> ("D8"),
    /// <c>ClassHeroSO.EntityId</c>, <c>IShopRewardEntry.EntryId</c>,
    /// <c>UpgradeSO.UpgradeId</c> o <c>RoomSO.RoomId</c>.
    /// </summary>
    public enum UnlockableCategory
    {
        /// <summary>Disponible en la pantalla de armado de build.</summary>
        Dice,

        /// <summary>Seleccionable en la pantalla de selección de personaje.</summary>
        HeroClass,

        /// <summary>Entra al pool de aparición en tiendas.</summary>
        ShopItem,

        /// <summary>Entra al pool de la Sala de Encantamiento.</summary>
        Enchantment,

        /// <summary>Entra al pool de generación de pisos.</summary>
        SpecialRoom,
    }
}
