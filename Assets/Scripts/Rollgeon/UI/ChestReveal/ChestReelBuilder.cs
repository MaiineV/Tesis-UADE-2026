using System.Collections.Generic;
using Rollgeon.Items;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Contenido de la tira del reel (puro, testeable): el ganador ya rolleado por
    /// el dominio va en <paramref name="winnerIndex"/>; el resto son fillers
    /// plausibles sampleados del pool preview del tier + celdas de oro. La UI solo
    /// renderiza — acá no hay probabilidad de reward real.
    /// </summary>
    public static class ChestReelBuilder
    {
        public static IReadOnlyList<ChestReelCellData> BuildStrip(
            ChestReelCellData winner,
            IReadOnlyList<ItemSO> poolPreview,
            ItemRarity tier,
            int totalCells,
            int winnerIndex,
            int goldFillerPerMille,
            int goldFillerMin,
            int goldFillerMax,
            System.Random rng)
        {
            if (totalCells < 1) totalCells = 1;
            if (winnerIndex < 0) winnerIndex = 0;
            if (winnerIndex >= totalCells) winnerIndex = totalCells - 1;

            var pool = CollectValidItems(poolPreview);
            int goldMin = goldFillerMin < 0 ? 0 : goldFillerMin;
            int goldMax = goldFillerMax < goldMin ? goldMin : goldFillerMax;

            var strip = new List<ChestReelCellData>(totalCells);
            ItemSO previousItem = null;

            for (int i = 0; i < totalCells; i++)
            {
                if (i == winnerIndex)
                {
                    strip.Add(winner);
                    previousItem = winner.Item;
                    continue;
                }

                // Pool vacío ⇒ todo filler es oro (degradación del contrato del payload).
                bool gold = pool.Count == 0 || rng.Next(1000) < goldFillerPerMille;
                if (gold)
                {
                    strip.Add(ChestReelCellData.ForGold(rng.Next(goldMin, goldMax + 1), tier));
                    previousItem = null;
                    continue;
                }

                var item = PickDistinctFromPrevious(pool, previousItem, rng);
                strip.Add(ChestReelCellData.ForItem(item));
                previousItem = item;
            }

            return strip;
        }

        private static List<ItemSO> CollectValidItems(IReadOnlyList<ItemSO> poolPreview)
        {
            var list = new List<ItemSO>();
            if (poolPreview == null) return list;
            for (int i = 0; i < poolPreview.Count; i++)
            {
                if (poolPreview[i] != null) list.Add(poolPreview[i]);
            }
            return list;
        }

        // Evita dos celdas adyacentes idénticas cuando el pool tiene variedad — un
        // reel con el mismo ícono repetido se lee como bug. Con 1 solo ítem no hay
        // alternativa y se repite.
        private static ItemSO PickDistinctFromPrevious(
            List<ItemSO> pool, ItemSO previous, System.Random rng)
        {
            var candidate = pool[rng.Next(pool.Count)];
            if (previous == null || pool.Count < 2) return candidate;

            int guard = 8;
            while (candidate == previous && guard-- > 0)
            {
                candidate = pool[rng.Next(pool.Count)];
            }
            if (candidate == previous)
            {
                // Fallback determinista: primer ítem distinto del anterior.
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i] != previous) return pool[i];
                }
            }
            return candidate;
        }
    }
}
