using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.ChestReveal
{
    /// <summary>
    /// Timings y layout del reveal gacha, externalizados (patrón
    /// <c>EnchantmentAltarUiSettingsSO</c> / <c>BreakdownAnimSettingsSO</c>). Las
    /// duraciones se dividen por <c>GameSpeedPrefs.Multiplier</c> en la vista —
    /// nunca tocar <c>Time.timeScale</c>.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Rollgeon/UI/Chest Reveal UI Settings",
        fileName = "ChestRevealUiSettings")]
    public sealed class ChestRevealUiSettingsSO : ScriptableObject
    {
        [Title("Panel")]
        [MinValue(0.1f)] public float OpenScaleFrom = 0.92f;
        [MinValue(0f)] public float OpenSeconds = 0.25f;
        [MinValue(0f)] public float CloseSeconds = 0.12f;

        [Title("Reel")]
        [MinValue(4)] public int TotalCells = 40;
        [MinValue(0)] public int MinSpinCells = 28;
        [MinValue(8f)] public float CellWidth = 96f;
        [MinValue(0f)] public float CellSpacing = 8f;
        [MinValue(0.1f)] public float SpinSeconds = 3.6f;

        [Tooltip("Curva de desaceleración del spin (t 0..1). Vacía = OutQuart default " +
                 "(ChestReelMath.Decelerate01).")]
        public AnimationCurve DecelerationCurve;

        [Range(0f, 0.9f)]
        [Tooltip("Jitter de aterrizaje: fracción del semiancho de celda. Nunca saca al " +
                 "puntero de la celda ganadora.")]
        public float MaxLandingJitter01 = 0.35f;

        [Range(0, 1000)]
        [Tooltip("Fracción (por mil) de celdas filler que son oro en vez de ítem.")]
        public int GoldFillerPerMille = 150;

        [MinValue(0)] public int GoldFillerMin = 5;
        [MinValue(0)] public int GoldFillerMax = 25;

        [Title("Reveal")]
        [MinValue(0f)] public float RevealFadeSeconds = 0.2f;
        [MinValue(0f)]
        [Tooltip("0 = espera el click del jugador para cerrar.")]
        public float AutoDismissSeconds = 0f;

        [Title("Skip / seguridad")]
        [MinValue(1f)] public float SkipSpeedMultiplier = 3f;
        [MinValue(1f)]
        [Tooltip("Watchdog: si la secuencia no terminó en este tiempo real, se fuerza el " +
                 "estado final y se libera el gate de turnos — nunca soft-lock.")]
        public float MaxSequenceSeconds = 12f;
    }
}
