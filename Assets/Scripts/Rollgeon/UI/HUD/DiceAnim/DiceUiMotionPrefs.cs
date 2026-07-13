using UnityEngine;

namespace Rollgeon.UI.HUD.DiceAnim
{
    /// <summary>
    /// Preferencia de accesibilidad "reduced motion" para el panel de dados: con
    /// el flag activo, <see cref="DiceZoneAnimator"/> corta TODAS las animaciones
    /// (spin, raise, outro, juice) y el flujo vuelve al comportamiento instantáneo
    /// legacy. Persistida en PlayerPrefs; toggle vía DevConsole (<c>dicemotion</c>)
    /// hasta que exista una pantalla de opciones.
    /// </summary>
    public static class DiceUiMotionPrefs
    {
        private const string Key = "Rollgeon.DiceReducedMotion";

        private static bool? _cached;

        public static bool ReducedMotion
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cached = null;
    }
}
