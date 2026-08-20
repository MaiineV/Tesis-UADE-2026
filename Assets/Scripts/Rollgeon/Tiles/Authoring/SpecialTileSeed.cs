using UnityEngine;

namespace Rollgeon.Tiles.Authoring
{
    /// <summary>
    /// Derivación determinista del seed de un slot de casilla especial. Clon del patrón
    /// <c>ChestSeed</c>, con dos diferencias deliberadas: el salt decorrela estos rolls de
    /// cofres/shop en la misma sala, y el hash del <c>SlotId</c> es propio (FNV-1a) para
    /// que agregar o quitar un slot no corra los rolls de los demás — y para no depender
    /// de <c>string.GetHashCode</c>, que no es estable entre procesos.
    /// </summary>
    public static class SpecialTileSeed
    {
        private const int Salt = 0x0057113; // "S7113" ≈ s-tile

        /// <summary>Puro para tests: mismo (floorSeed, celda, slotId) → misma elección.</summary>
        public static int Derive(int floorSeed, Vector2Int cell, string slotId)
        {
            unchecked
            {
                return (floorSeed * 92821 + cell.x * 31 + cell.y) ^ Salt ^ Fnv1a(slotId);
            }
        }

        private static int Fnv1a(string value)
        {
            unchecked
            {
                const int prime = 16777619;
                int hash = (int)2166136261;
                if (string.IsNullOrEmpty(value)) return hash;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= prime;
                }
                return hash;
            }
        }
    }
}
