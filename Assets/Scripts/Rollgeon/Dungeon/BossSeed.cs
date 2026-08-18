using UnityEngine;

namespace Rollgeon.Dungeon
{
    /// <summary>
    /// Derivación determinista del seed con el que se rolea el boss de una sala. Mismo estilo
    /// que <see cref="Chests.ChestSeed"/> y <c>ShopManagerService.DeriveShopSeed</c> — se mezcla
    /// (floorSeed, celda de la sala) y NUNCA el <c>InstanceId</c>, que se regenera en cada
    /// <c>GenerateFloor</c> y rompería el resume.
    /// </summary>
    /// <remarks>
    /// El RNG propio es lo que deja el boss determinístico por seed <b>sin</b> mover la secuencia
    /// del rng principal del planner: los pisos que ya existían para un seed dado siguen saliendo
    /// idénticos. El salt decorrela el roll del boss de los del cofre y el shop en la misma celda.
    /// </remarks>
    public static class BossSeed
    {
        private const int Salt = 0x00B0550; // "B0550" ≈ boss

        /// <summary>Puro para tests: mismo (floorSeed, celda) → mismo boss.</summary>
        public static int Derive(int floorSeed, Vector2Int cell)
        {
            unchecked
            {
                return (floorSeed * 92821 + cell.x * 31 + cell.y) ^ Salt;
            }
        }
    }
}
