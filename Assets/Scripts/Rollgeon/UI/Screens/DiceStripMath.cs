using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Lógica pura de la tira de dados de <see cref="BuildSelectionScreen"/>:
    /// orden ascendente por caras y diff entre estados consecutivos de la
    /// bolsa. Sin dependencias de UnityEngine.Object — testeable en EditMode.
    /// </summary>
    public static class DiceStripMath
    {
        public enum StripChange
        {
            /// <summary>Sin diferencia.</summary>
            None,

            /// <summary>Se insertó un dado en <c>Index</c>.</summary>
            Insert,

            /// <summary>Se quitó el dado que estaba en <c>Index</c>.</summary>
            Remove,

            /// <summary>Cambio mayor (limpiar, resync) — reconstruir la tira.</summary>
            Rebuild,
        }

        public readonly struct StripDiff
        {
            public readonly StripChange Change;
            public readonly int Index;

            public StripDiff(StripChange change, int index)
            {
                Change = change;
                Index = index;
            }
        }

        /// <summary>Copia ordenada de menor a mayor por <see cref="DiceTypeExt.MaxFace"/>.</summary>
        public static List<DiceType> SortAscending(IReadOnlyList<DiceType> bag)
        {
            var sorted = new List<DiceType>(bag ?? (IReadOnlyList<DiceType>)System.Array.Empty<DiceType>());
            sorted.Sort((a, b) => a.MaxFace().CompareTo(b.MaxFace()));
            return sorted;
        }

        /// <summary>
        /// Diff entre dos listas ORDENADAS de la bolsa. La UI solo agrega o quita
        /// de a uno, así que el caso normal es una inserción/remoción única en la
        /// primera divergencia; cualquier otra cosa degrada a Rebuild.
        /// </summary>
        public static StripDiff ComputeDiff(IReadOnlyList<DiceType> oldSorted, IReadOnlyList<DiceType> newSorted)
        {
            oldSorted ??= System.Array.Empty<DiceType>();
            newSorted ??= System.Array.Empty<DiceType>();

            int delta = newSorted.Count - oldSorted.Count;

            if (delta == 0)
            {
                for (int i = 0; i < oldSorted.Count; i++)
                {
                    if (oldSorted[i] != newSorted[i]) return new StripDiff(StripChange.Rebuild, -1);
                }
                return new StripDiff(StripChange.None, -1);
            }

            if (delta == 1) return TrySingleEdit(oldSorted, newSorted, insert: true);
            if (delta == -1) return TrySingleEdit(newSorted, oldSorted, insert: false);

            return new StripDiff(StripChange.Rebuild, -1);
        }

        // shorter/longer difieren en exactamente 1 elemento: encontrar el índice
        // de la edición y validar que el resto coincida.
        private static StripDiff TrySingleEdit(
            IReadOnlyList<DiceType> shorter, IReadOnlyList<DiceType> longer, bool insert)
        {
            int index = shorter.Count; // default: la edición está al final
            for (int i = 0; i < shorter.Count; i++)
            {
                if (shorter[i] != longer[i])
                {
                    index = i;
                    break;
                }
            }

            for (int i = index; i < shorter.Count; i++)
            {
                if (shorter[i] != longer[i + 1]) return new StripDiff(StripChange.Rebuild, -1);
            }

            return new StripDiff(insert ? StripChange.Insert : StripChange.Remove, index);
        }

        /// <summary>Posición X centrada del dado <paramref name="index"/> en una tira de <paramref name="count"/>.</summary>
        public static float SlotX(int index, int count, float spacing)
        {
            return (index - (count - 1) * 0.5f) * spacing;
        }

        /// <summary>
        /// Spacing efectivo para que <paramref name="count"/> dados entren en
        /// <paramref name="availableWidth"/>: el autorado manda mientras quepa; si
        /// no, se comprime (abanico con solape) sin volverse negativo. Con 5 exactos
        /// nunca clampa, pero la bolsa ya no tiene tope por tipo y puede crecer.
        /// </summary>
        public static float FitSpacing(int count, float baseSpacing, float dieSize, float availableWidth)
        {
            if (count <= 1) return baseSpacing;
            float fit = (availableWidth - dieSize) / (count - 1);
            if (fit >= baseSpacing) return baseSpacing;
            return fit > 0f ? fit : 0f;
        }
    }
}
