using UnityEngine;

namespace Rollgeon.Timing
{
    /// <summary>
    /// Velocidad de RESOLUCIÓN del juego (x1/x2/x4/x8) persistida en PlayerPrefs.
    /// NUNCA toca <c>Time.timeScale</c> — eso aceleraba animators, partículas y
    /// shaders y volvía ilegible el combate. En cambio, los consumidores dividen
    /// sus duraciones de pacing por <see cref="Multiplier"/>: la secuencia del
    /// breakdown (vía <c>D()</c> del director), los delays puros entre acciones
    /// (turno enemigo, gaps autorados de feedback) y los holds/staggers de la
    /// fase de dados. Las animaciones y efectos corren siempre a velocidad real.
    /// </summary>
    public static class GameSpeedPrefs
    {
        private const string Key = "Rollgeon.GameSpeed";

        public static readonly int[] Speeds = { 1, 2, 4, 8 };

        private static int? _cached;

        public static int Multiplier
        {
            get
            {
                _cached ??= SanitizeSpeed(PlayerPrefs.GetInt(Key, 1));
                return _cached.Value;
            }
            set
            {
                _cached = SanitizeSpeed(value);
                PlayerPrefs.SetInt(Key, _cached.Value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Avanza al siguiente speed del ciclo y lo aplica. Devuelve el nuevo.</summary>
        public static int CycleNext() => Multiplier = NextSpeed(Multiplier);

        /// <summary>1→2→4→8→1; un valor fuera del ciclo resetea a 1.</summary>
        public static int NextSpeed(int current)
        {
            for (int i = 0; i < Speeds.Length; i++)
            {
                if (Speeds[i] == current)
                    return Speeds[(i + 1) % Speeds.Length];
            }
            return Speeds[0];
        }

        /// <summary>Colapsa cualquier valor persistido/pasado inválido a x1.</summary>
        public static int SanitizeSpeed(int raw)
        {
            for (int i = 0; i < Speeds.Length; i++)
            {
                if (Speeds[i] == raw) return raw;
            }
            return Speeds[0];
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cached = null;
    }
}
