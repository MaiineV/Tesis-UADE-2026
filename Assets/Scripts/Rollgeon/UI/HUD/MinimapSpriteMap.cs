using Rollgeon.Dungeon;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Mapeo estado→sprite del minimapa (índices del sheet
    /// <c>Assets/Art/UI/Minimap/Minimap.png</c>, slices <c>Minimap_0..8</c>):
    /// <list type="bullet">
    ///   <item>0 = adyacente sin visitar (sala normal)</item>
    ///   <item>1 = sala actual (normal)</item>
    ///   <item>2 = visitada no-actual (normal)</item>
    ///   <item>3 = actual tienda · 6 = tienda no-actual (visitada o sin visitar)</item>
    ///   <item>4 = actual encantamientos · 5 = encantamientos no-actual</item>
    ///   <item>8 = actual boss · 7 = boss no-actual</item>
    /// </list>
    /// Start/Combat/Potion usan la terna normal 0/1/2. Que las salas especiales
    /// adyacentes sin visitar muestren su ícono es deliberado (spec del minimapa).
    /// </summary>
    public static class MinimapSpriteMap
    {
        /// <summary>Índice 0..8 del slice a usar. IsCurrent gana sobre IsVisited.</summary>
        public static int Resolve(RoomType type, bool isCurrent, bool isVisited)
        {
            switch (type)
            {
                case RoomType.Boss: return isCurrent ? 8 : 7;
                case RoomType.Shop: return isCurrent ? 3 : 6;
                case RoomType.Enchantment: return isCurrent ? 4 : 5;
                default: return isCurrent ? 1 : (isVisited ? 2 : 0);
            }
        }

        public static int Resolve(in MinimapCell cell)
            => Resolve(cell.Type, cell.IsCurrent, cell.IsVisited);
    }
}
