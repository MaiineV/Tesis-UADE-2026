using System.Collections.Generic;
using Rollgeon.Combos;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Tier visual de la llama de combo armado (<see cref="DiceFlameView"/>): <see cref="Low"/>
    /// para la mitad baja del catálogo de combos, <see cref="High"/> para la mitad alta. Se decide
    /// por POSICIÓN en el catálogo ordenado por <c>Priority</c> y no por el valor de Priority:
    /// balance retunea prioridades y el corte tiene que seguir siendo "la mitad de la tabla".
    /// </summary>
    public static class ComboFlameTier
    {
        /// <summary>Sin combo armado: la llama no se muestra.</summary>
        public const int Off = 0;

        /// <summary>Mitad baja del catálogo (Higher Number, Par, Doble Par, Trío con 8 combos).</summary>
        public const int Low = 1;

        /// <summary>Mitad alta del catálogo (Full House, Escalera, Póker, Generala con 8 combos).</summary>
        public const int High = 2;

        /// <summary>
        /// Resuelve el tier de la llama para un combo armado. Sin id no hay llama; sin catálogo
        /// o con un id que el catálogo no conoce cae a <see cref="Low"/>: que haya fuego pesa más
        /// que acertar el tier (el HUD detecta contra el ContractSheet del héroe y el catálogo es
        /// solo el fallback, y los tests corren sin ServiceLocator).
        /// </summary>
        public static int Resolve(ComboCatalogSO catalog, string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return Off;
            if (catalog == null) return Low;

            int rank = RankOf(catalog.Entries, comboId, out int count);
            if (rank < 0) return Low;
            return rank >= (count + 1) / 2 ? High : Low;
        }

        /// <summary>
        /// Posición del combo en el catálogo ordenado por Priority ascendente (empates en orden
        /// de catálogo, como un sort estable), ignorando entradas nulas. <c>-1</c> si no está.
        /// Cuenta en vez de ordenar: sin allocs y el mismo resultado que un OrderBy estable.
        /// </summary>
        private static int RankOf(IReadOnlyList<BaseComboSO> entries, string comboId, out int count)
        {
            count = 0;
            if (entries == null) return -1;

            int targetIndex = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                count++;
                if (targetIndex < 0 && entry.ComboId == comboId) targetIndex = i;
            }
            if (targetIndex < 0) return -1;

            int targetPriority = entries[targetIndex].Priority;
            int rank = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || i == targetIndex) continue;
                if (entry.Priority < targetPriority
                    || (entry.Priority == targetPriority && i < targetIndex))
                    rank++;
            }
            return rank;
        }
    }
}
