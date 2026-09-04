using PrimeTween;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Tuning visual/juice de la view del Altar de Encantamiento: apertura y
    /// cierre del panel, entrada escalonada de las cards, hover/selección y el
    /// feedback del resultado. El installer <c>Rollgeon → Enchantment Altar</c>
    /// crea el asset en <c>Assets/Rollgeon/Services/</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Enchantment Altar UI Settings", fileName = "EnchantmentAltarUiSettings")]
    public class EnchantmentAltarUiSettingsSO : ScriptableObject
    {
        [Header("Panel — abrir/cerrar")]
        [Tooltip("Escala inicial desde la que el panel crece al abrir.")]
        public float OpenScaleFrom = 0.92f;

        public float OpenDuration = 0.25f;
        public Ease OpenEase = Ease.OutBack;

        public float CloseDuration = 0.12f;
        public Ease CloseEase = Ease.InQuad;

        [Header("Panel — backdrop")]
        [Tooltip("Alpha del telón oscuro que separa la mesa del gameplay mientras está abierta. " +
                 "0 = sin backdrop.")]
        [Range(0f, 1f)] public float BackdropAlpha = 0.55f;

        [Header("Cards — entrada escalonada")]
        [Tooltip("Escala inicial de cada card al entrar (escala+alpha — no posición, para no pelear con el layout group).")]
        public float CardEnterScaleFrom = 0.9f;

        public float CardEnterDuration = 0.2f;
        public Ease CardEnterEase = Ease.OutCubic;

        [Tooltip("Delay incremental entre cards.")]
        public float CardStagger = 0.04f;

        [Header("Cards — hover y selección")]
        public float HoverScale = 1.05f;
        public float HoverDuration = 0.1f;

        [Tooltip("Escala extra del punch al seleccionar (0.08 = +8%).")]
        public float SelectPunchScale = 0.08f;

        public float SelectPunchDuration = 0.18f;

        [Header("Resultado del encantamiento")]
        [Tooltip("Offset vertical (px) del slide-in del label de resultado.")]
        public float ResultSlideY = 8f;

        public float ResultFadeDuration = 0.2f;
        public Ease ResultEase = Ease.OutCubic;

        [Header("Slot machine — palanca")]
        [Tooltip("Bajada: squash del sprite desde la altura de reposo hasta la del frame bajado.")]
        public float LeverPressDuration = 0.09f;

        [Tooltip("Tiempo que la palanca queda abajo antes de volver.")]
        public float LeverHoldDuration = 0.12f;

        [Tooltip("Vuelta: más suave que la bajada, como una palanca con resorte.")]
        public float LeverReturnDuration = 0.3f;
        public Ease LeverReturnEase = Ease.OutCubic;

        [Header("Slot machine — reels")]
        [Tooltip("Duración del giro del primer reel; cada reel siguiente suma el stagger.")]
        public float ReelSpinDuration = 1.1f;

        [Tooltip("Extra de duración por reel — paran de a uno, izquierda a derecha.")]
        public float ReelStopStagger = 0.55f;

        [Tooltip("Cantidad de swaps de nombre del primer reel (la desaceleración los espacia hacia el final, como una slot real).")]
        public int ReelTotalCycles = 16;

        [Tooltip("Punch de escala del slot al aterrizar su encantamiento.")]
        public float ReelLandPunchScale = 0.12f;

        public float ReelLandPunchDuration = 0.2f;

        [Header("Slot machine — dados en la repisa")]
        [Tooltip("Cuánto sube el dado seleccionado (px).")]
        public float DieSelectRise = 12f;

        public float DieSelectRiseDuration = 0.15f;
        public Ease DieSelectRiseEase = Ease.OutBack;

        [Tooltip("Outline del dado SELECCIONADO — solo el contorno, no el shader completo. " +
                 "Tono arcano distinto del dorado de 'clickeable'.")]
        public Color DieSelectedOutlineColor = new Color32(0xB0, 0x7B, 0xFF, 0xFF);

        public Vector2 DieSelectedOutlineDistance = new Vector2(4f, -4f);

        [Header("Slot machine — carousel de sets (Ataque ↔ Movimiento)")]
        [Tooltip("Duración del giro entre el set de Ataque y el de Movimiento.")]
        public float SetSwitchDuration = 0.25f;

        public Ease SetSwitchEase = Ease.OutCubic;

        [Tooltip("Cuánto se desplaza en X el set que sale / desde dónde entra el otro (px).")]
        public float SetSwitchSlideX = 220f;

        [Header("Slot machine — botón Confirmar")]
        [Tooltip("Medio ciclo del pulso de brillo (dim→bright). El loop es infinito mientras esté listo.")]
        public float ConfirmPulseHalfDuration = 0.9f;

        [Tooltip("Brillo mínimo del pulso (multiplica el color de la Image).")]
        [Range(0f, 1f)] public float ConfirmPulseMinBrightness = 0.5f;

        [Tooltip("Alpha máximo del glow (outline dorado) en el pico del pulso — el 'más brilloso'.")]
        [Range(0f, 1f)] public float ConfirmPeakGlowAlpha = 0.9f;

        [Tooltip("Por debajo de este valor del pulso (0..1), el botón muestra el sprite apagado — " +
                 "el parpadeo _0 ↔ _2 en el valle del brillo.")]
        [Range(0f, 1f)] public float ConfirmPulseSwapThreshold = 0.12f;

        [Header("Slot machine — outline de clickeable")]
        [Tooltip("Color del outline al hoverar palanca / opciones (señal de 'esto se clickea'). " +
                 "Dorado claro — tiene que contrastar con el sprite de la palanca.")]
        public Color ClickableOutlineColor = new Color32(0xFF, 0xD7, 0x5A, 0xFF);

        public Vector2 ClickableOutlineDistance = new Vector2(3f, -3f);
    }
}
