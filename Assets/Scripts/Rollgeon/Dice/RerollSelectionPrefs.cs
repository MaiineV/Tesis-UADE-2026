using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Preferencia de semántica de selección para el reroll. Default (false) es el
    /// modo invertido estilo Balatro: los dados seleccionados son los que se
    /// re-tiran. Con <see cref="KeepSelected"/> activo rige el modo clásico: los
    /// seleccionados se quedan y vuelan los demás. Persistida en PlayerPrefs;
    /// toggle desde la pantalla de opciones.
    /// </summary>
    public static class RerollSelectionPrefs
    {
        private const string Key = "Rollgeon.RerollKeepSelected";

        private static bool? _cached;

        public static bool KeepSelected
        {
            get
            {
                _cached ??= PlayerPrefs.GetInt(Key, 0) != 0;
                return _cached.Value;
            }
            set
            {
                _cached = value;
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Mapea la selección de la UI (holds) a la máscara física de keep del
        /// roller según el modo vigente. Invertido: keep = complemento de la
        /// selección, y un índice sin estado de selección se conserva (un dado que
        /// el jugador no pudo marcar no debe volar). Clásico: keep = la selección,
        /// y un índice sin estado vuela (no está lockeado).
        /// </summary>
        public static bool[] SelectionToKeep(bool[] selected, int diceLen)
        {
            int len = diceLen > 0 ? diceLen : (selected?.Length ?? 0);
            bool keepSelected = KeepSelected;
            var keep = new bool[len];
            for (int i = 0; i < len; i++)
            {
                bool sel = selected != null && i < selected.Length && selected[i];
                keep[i] = keepSelected ? sel : !sel;
            }
            return keep;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cached = null;
    }
}
