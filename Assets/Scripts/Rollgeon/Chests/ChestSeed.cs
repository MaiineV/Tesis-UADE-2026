using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Derivación determinista del seed de rolls del cofre. Mismo estilo que
    /// <c>ShopManagerService.DeriveShopSeed</c> — se mezcla (floorSeed, celda de la
    /// sala) y NUNCA el <c>InstanceId</c> (se regenera en cada GenerateFloor, rompería
    /// resume). El salt decorrela los rolls del cofre de los del shop para la misma sala.
    /// </summary>
    public static class ChestSeed
    {
        private const int Salt = 0x00C4E57; // "C4E57" ≈ chest

        /// <summary>Puro para tests: mismo (floorSeed, celda) → mismos rolls.</summary>
        public static int Derive(int floorSeed, Vector2Int cell)
        {
            unchecked
            {
                return (floorSeed * 92821 + cell.x * 31 + cell.y) ^ Salt;
            }
        }
    }
}
