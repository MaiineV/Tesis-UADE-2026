using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Perillas de la secuencia de breakdown (patrón <c>DiceUiAnimationSettingsSO</c>):
    /// tiempos cortos y secos estilo Balatro, tuneables por diseño sin recompilar.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Breakdown Anim Settings",
        fileName = "BreakdownAnimSettings")]
    public class BreakdownAnimSettingsSO : ScriptableObject
    {
        [Title("Vuelos")]
        [MinValue(0.05f), Tooltip("Duración del vuelo de un valor (player base / cara de dado).")]
        public float FlightSeconds = 0.32f;

        [Tooltip("Curvatura del vuelo (px perpendiculares al segmento).")]
        public float FlightArc = 60f;

        [MinValue(0.05f), Tooltip("Duración del vuelo de un proc de dado / global (curva más pronunciada).")]
        public float ProcFlightSeconds = 0.38f;

        public float ProcFlightArc = 110f;

        [MinValue(0f), Tooltip("Pausa entre pasos consecutivos.")]
        public float StepGapSeconds = 0.08f;

        [Title("Spinner (globales)")]
        [MinValue(0f), Tooltip("Rotación del tambor al siguiente modificador (tras el vuelo del número).")]
        public float SpinnerSpinSeconds = 0.22f;

        [Title("Choque final")]
        [MinValue(0.05f), Tooltip("Viaje de N y M hacia el punto de choque (acelerando).")]
        public float ClashTravelSeconds = 0.22f;

        [MinValue(0f), Tooltip("Hold del total tras el choque, antes de liberar el golpe.")]
        public float ClashHoldSeconds = 0.4f;

        [Title("Mitigación (post-choque)")]
        [MinValue(0.05f), Tooltip("Vuelo del '-X' de mitigación hacia el total.")]
        public float MitigationSeconds = 0.3f;

        [Title("Skip / seguridad")]
        [MinValue(1f), Tooltip("Multiplicador de velocidad del primer click de skip.")]
        public float SkipSpeedMultiplier = 3f;

        [MinValue(1f), Tooltip("Presupuesto duro de la secuencia completa — vencido, se " +
                               "fuerza el estado final y se libera el gate.")]
        public float MaxSequenceSeconds = 8f;

        // ====================================================================
        // Feel — identidad de color y perillas de juice (backlog 2026-08-10)
        // ====================================================================

        [Title("Colores semánticos")]
        [Tooltip("Color de N y de todo lo que vuela hacia N (aditivos).")]
        public Color CounterNColor = new Color32(0x4F, 0xA8, 0xFF, 0xFF);

        [Tooltip("Color de M cuando vale 1.0 (apagado — todavía no hay multiplicador).")]
        public Color CounterMNeutralColor = new Color32(0x8A, 0x8F, 0x98, 0xFF);

        [Tooltip("Color de M entre 1 y 2 (y de lo que vuela hacia M).")]
        public Color CounterMWarmColor = new Color32(0xFF, 0x6B, 0x47, 0xFF);

        [Tooltip("Color de M a partir de 2 (rojo fuego).")]
        public Color CounterMHotColor = new Color32(0xFF, 0x3B, 0x2F, 0xFF);

        [Tooltip("Flash del total cuando la weakness lo sube (paleta DamageWeakness).")]
        public Color WeaknessColor = new Color32(0xFF, 0xD7, 0x5A, 0xFF);

        [Tooltip("Tinte del total durante el tick-down de mitigación (paleta Escudo).")]
        public Color MitigationColor = new Color32(0xA3, 0xB3, 0xB1, 0xFF);

        [Tooltip("Porción de cara plana en el '+N' bajo el dado (blanco hueso).")]
        public Color ContributionFaceColor = new Color32(0xF5, 0xEF, 0xE0, 0xFF);

        [Tooltip("Porción de bono de encantamiento en el '+N' (dorado = bonus).")]
        public Color ContributionBonusColor = new Color32(0xFF, 0xD7, 0x5A, 0xFF);

        [Title("Preview")]
        [MinValue(0f), Tooltip("Pop-in OutBack del N×M al aparecer (0 = sin pop).")]
        public float PreviewPopSeconds = 0.18f;

        [MinValue(0f), Tooltip("Roll-up de los contadores al cambiar el preview por hold.")]
        public float PreviewRollupSeconds = 0.15f;

        [MinValue(0f), Tooltip("Idle wobble de M (±grados a amplitud máxima; escala con M−1).")]
        public float WobbleMaxDegrees = 3f;

        [MinValue(0.1f), Tooltip("Frecuencia del wobble en Hz.")]
        public float WobbleHz = 1.4f;

        [MinValue(0f), Tooltip("Stagger entre los '+N' de cada dado al aparecer.")]
        public float ContributionStaggerSeconds = 0.05f;

        [Title("Ramp de dados")]
        [MinValue(0f), Tooltip("Subida de pitch por dado consecutivo (~0.06 = 1 semitono).")]
        public float DiePitchStep = 0.06f;

        [Range(0f, 1f), Tooltip("Dim del dado ya 'gastado' (multiplica su color hasta el fin).")]
        public float SpentDieDim = 0.7f;

        [Title("Ramp de pasos")]
        [MinValue(0f), Tooltip("Aceleración por paso resuelto: cada step recorta este % del " +
                               "tiempo de los siguientes (Balatro). Aplica a base/dados/procs/" +
                               "globales — el clash y la mitigación quedan a ritmo pleno.")]
        public float StepSpeedRampPerStep = 0.07f;

        [Range(0.1f, 1f), Tooltip("Piso del ramp por paso (fracción mínima del tiempo original).")]
        public float StepSpeedFloor = 0.45f;

        [Title("Punches / anticipación")]
        [MinValue(1f), Tooltip("Multiplicador del punch del contador a aporte máximo.")]
        public float PunchIntensityMax = 2f;

        [MinValue(0f), Tooltip("Jiggle rotacional máximo del contador (±grados).")]
        public float PunchRotationMaxDegrees = 4f;

        [MinValue(0f), Tooltip("Wind-up de la espada antes de que despegue su valor (0 = off).")]
        public float SwordWindupSeconds = 0.10f;

        [MinValue(0f), Tooltip("Popup del proc (glow + sfx) antes de que su valor vuele (0 = off).")]
        public float ProcPopupSeconds = 0.12f;

        [Title("Clash")]
        [MinValue(0f), Tooltip("Anticipación: N y M se separan hacia afuera antes de lanzarse.")]
        public float ClashWindupSeconds = 0.08f;

        [MinValue(0f), Tooltip("Píxeles de separación del wind-up.")]
        public float ClashWindupPixels = 12f;

        [MinValue(0f), Tooltip("Roll-up del total tras el choque (0 = slam directo).")]
        public float ClashRollupSeconds = 0.25f;

        [Range(0f, 1f), Tooltip("Alpha pico del flash de pantalla en el impacto.")]
        public float FlashPeakAlpha = 0.15f;

        [MinValue(0f), Tooltip("Duración del flash de pantalla.")]
        public float FlashSeconds = 0.12f;

        [MinValue(0f), Tooltip("Hitstop del impacto (DiceHitstop, escala por tier).")]
        public float HitstopSeconds = 0.06f;

        [MinValue(0f), Tooltip("Amplitud base del camera shake (tier medio).")]
        public float ShakeAmplitude = 0.6f;

        [MinValue(0f), Tooltip("Duración del camera shake.")]
        public float ShakeSeconds = 0.25f;

        [MinValue(0), Tooltip("Total < T1 → tier chico.")]
        public int TierThreshold1 = 30;

        [MinValue(0), Tooltip("Total ≥ T2 → tier grande.")]
        public int TierThreshold2 = 80;

        [MinValue(0f)] public float TierScaleSmall = 0.5f;
        [MinValue(0f)] public float TierScaleMedium = 1f;
        [MinValue(0f)] public float TierScaleBig = 1.7f;

        [Title("Celebración de combo (dados contribuyentes)")]
        [MinValue(0), Tooltip("Partículas del burst por dado a intensidad mínima.")]
        public int ComboBurstCountMin = 36;

        [MinValue(0), Tooltip("Partículas del burst por dado a intensidad máxima.")]
        public int ComboBurstCountMax = 80;

        [MinValue(0f), Tooltip("Punch de escala del dado (magnitud a intensidad máxima).")]
        public float ComboPunchScale = 0.18f;

        [MinValue(0f), Tooltip("Shake de rotación del dado (grados a intensidad máxima).")]
        public float ComboShakeRotation = 10f;

        [MinValue(0f), Tooltip("Stagger entre dados contribuyentes (se divide por el game speed).")]
        public float ComboCelebrateStagger = 0.05f;

        [MinValue(1f), Tooltip("M mínimo para que el total 'arda' (flaming number).")]
        public float FlamingNumberMinM = 2f;

        [Range(0f, 1f), Tooltip("Factor de duck de música alrededor del choque (1 = sin duck).")]
        public float ClashDuckFactor = 0.4f;

        [MinValue(0f)] public float ClashDuckFadeSeconds = 0.15f;

        [Title("Mitigación")]
        [MinValue(0f), Tooltip("Tinte azul-gris del total durante el tick-down.")]
        public float MitigationTickDownSeconds = 0.3f;

        [Title("Toggles de juice (debug / accesibilidad)")]
        public bool EnableSfx = true;
        public bool EnableParticles = true;
        public bool EnableShakeAndHitstop = true;
    }
}
