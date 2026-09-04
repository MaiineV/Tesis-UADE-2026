using Rollgeon.Localization;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Texto localizado de un <see cref="RoomType"/> — key <c>room.type.&lt;lower&gt;</c>
    /// de la tabla UI (sembrada por <c>LocalizationContentSeeder</c>). Compartido por el
    /// HUD de navegación y el toast de sala para que ambos digan lo mismo.
    /// </summary>
    public static class RoomTypeText
    {
        public static string Key(RoomType type) => "room.type." + type.ToString().ToLowerInvariant();

        public static string Localized(RoomType type) => LocalizedContent.Ui(Key(type), type.ToString());
    }
}
