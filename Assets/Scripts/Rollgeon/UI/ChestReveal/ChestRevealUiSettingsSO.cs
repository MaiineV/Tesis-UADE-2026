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

        // ------------------------------------------------------------------
        // Juice — los knobs por-rareza se lerpean con ChestRevealFeelMath.Knob
        // sobre Intensity01(tier). ReducedMotion apaga movimiento, no audio.
        // ------------------------------------------------------------------

        [Title("Juice — toggles (debug / accesibilidad)")]
        public bool EnableSfx = true;
        public bool EnableParticles = true;
        public bool EnableShakeAndHitstop = true;

        [Title("Juice — open/close")]
        [MinValue(0f)] public float DimFadeSeconds = 0.15f;
        [Range(0f, 1f)]
        [Tooltip("Alpha de reposo del dim — debe coincidir con el look del prefab.")]
        public float DimRestAlpha = 0.6f;
        [Tooltip("Tilt Z inicial del panel que se asienta a 0 durante el open.")]
        public float OpenTiltDegrees = 4f;
        [MinValue(0f)] public float TitlePunchScale = 0.15f;
        [MinValue(0f)] public float TitlePunchSeconds = 0.18f;
        [Range(0.5f, 1f)] public float ClosePanelScaleTo = 0.94f;

        [Title("Juice — spin")]
        public float TickBasePitch = 0.9f;
        public float TickMaxPitch = 1.5f;
        [MinValue(0.005f)]
        [Tooltip("Intervalo mínimo real entre ticks — se divide por la velocidad de juego.")]
        public float TickMinInterval = 0.03f;
        [Range(0f, 1f)] public float TickVolume = 0.5f;
        [MinValue(0f)] public float PointerFlickDegrees = 10f;
        [MinValue(0.01f)] public float PointerFlickSeconds = 0.08f;
        [Range(0.5f, 0.99f)]
        [Tooltip("Progreso del spin en el que dispara la anticipación del landing (una vez).")]
        public float ClimaxT = 0.85f;
        [MinValue(1f)] public float ClimaxZoomScale = 1.03f;
        [MinValue(0f)] public float ClimaxZoomSeconds = 0.6f;
        [Range(0f, 1f)]
        [Tooltip("Duck de música durante el climax (solo Rare+). 1 = sin duck.")]
        public float ClimaxDuckFactor = 0.7f;

        [Title("Juice — reveal (min→max lerp por rareza)")]
        [MinValue(0f)] public float FlashSeconds = 0.15f;
        [Range(0f, 0.35f)] public float FlashPeakAlphaMax = 0.25f;
        [Range(0f, 1f)] public float BurstIntensityMin = 0.2f;
        [Range(0f, 1f)] public float BurstIntensityMax = 1f;
        [MinValue(0f)] public float PanelShakeAmplitudeMax = 7f;
        [MinValue(0f)] public float PanelShakeSeconds = 0.3f;
        [MinValue(1f)] public float PanelShakeFrequency = 12f;
        [MinValue(0f)] public float CamShakeAmplitudeMax = 0.6f;
        [MinValue(0f)] public float CamShakeSeconds = 0.25f;
        [MinValue(0f)]
        [Tooltip("Micro-hitstop del landing — solo Legendary.")]
        public float HitstopSeconds = 0.05f;
        public float ChimePitchMin = 0.9f;
        public float ChimePitchMax = 1.25f;
        [MinValue(1f)] public float WinnerPunchScaleMin = 1.10f;
        [MinValue(1f)] public float WinnerPunchScaleMax = 1.25f;
        [MinValue(0.01f)] public float WinnerPunchSeconds = 0.18f;
        [Range(0f, 1f)]
        [Tooltip("Duck de música mientras se muestra el reward (solo Rare+). 1 = sin duck.")]
        public float RevealDuckFactor = 0.5f;

        [Title("Juice — card / idle")]
        [Range(0.1f, 1f)] public float CardPopScaleFrom = 0.8f;
        [MinValue(0f)] public float CardPopSeconds = 0.22f;
        [MinValue(0f)]
        [Tooltip("Duración del count-up del oro. 0 = sin count-up (texto directo).")]
        public float GoldCountUpSeconds = 0.35f;
        [MinValue(0.1f)] public float IdlePulsePeriod = 0.9f;
        [Range(0f, 1f)]
        [Tooltip("Cuánto se acerca a blanco el marco del ganador en el pulse idle.")]
        public float IdlePulseColorLerp = 0.35f;
        [MinValue(1f)] public float IdlePulseScale = 1.03f;
    }
}
