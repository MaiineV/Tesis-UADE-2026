using UnityEngine;

namespace Rollgeon.Timing
{
    /// <summary>
    /// Velocidad global del juego (x1/x2/x4/x8) persistida en PlayerPrefs y
    /// aplicada vía <c>Time.timeScale</c>. Única fuente de verdad del timeScale
    /// "en reposo": <c>DiceHitstop</c> restaura leyendo <see cref="Multiplier"/>,
    /// no una copia local. Los menús/overlays corren unscaled, así que no se
    /// aceleran — solo la simulación (feedbacks, turnos, tweens scaled).
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
                ApplyToTimeScale();
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

        public static void ApplyToTimeScale()
        {
            // timeScale en 0 = freeze de DiceHitstop en curso (nadie más lo deja
            // ahí): no pisarlo — al descongelar, Restore() lee Multiplier y el
            // valor nuevo entra solo.
            if (Time.timeScale > 0f)
                Time.timeScale = Multiplier;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cached = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyAtBoot() => ApplyToTimeScale();
    }
}
