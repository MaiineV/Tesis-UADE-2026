using UnityEngine;

namespace Rollgeon.UI.Menu
{
    /// <summary>
    /// Tuning del juice de menú (main menu y pausa). Defaults calcados del
    /// video de referencia de <c>video/menu.mp4</c>; la paleta viene del
    /// Figma del proyecto.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Menu Juice Settings", fileName = "MenuJuiceSettings")]
    public class MenuJuiceSettingsSO : ScriptableObject
    {
        [Header("Paleta")]
        public Color TextColor = new Color32(0xE7, 0xE3, 0xE2, 0xFF);
        public Color AccentColor = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
        public Color MutedColor = new Color32(0x5F, 0x73, 0x7A, 0xFF);

        [Tooltip("Color de los botones no hovereados mientras otro tiene el foco.")]
        public Color DimColor = new Color(0x5F / 255f, 0x73 / 255f, 0x7A / 255f, 0.55f);

        [Header("Entrada (stagger)")]
        public float StaggerStep = 0.15f;
        public float EntranceDuration = 0.4f;

        [Tooltip("Distancia hacia abajo desde la que entra cada botón.")]
        public float EntranceYOffset = 40f;

        [Header("Hover")]
        public float HoverScale = 1.15f;
        public float ScaleLerpSpeed = 8f;

        [Tooltip("Desplazamiento X del botón hovereado (negativo = izquierda, como el video).")]
        public float HoverXOffset = -12f;
        public float OffsetLerpSpeed = 10f;

        [Header("Subrayado")]
        public float UnderlinePad = 12f;
        public float UnderlineLerpSpeed = 14f;

        [Header("Ciclo de color en hover")]
        [Tooltip("Velocidad del ciclo pastel entre TextColor, AccentColor y MutedColor.")]
        public float ColorCycleSpeed = 5f;
        public float ColorLerpSpeed = 10f;

        [Header("Punch al click")]
        public float PunchScale = 1.3f;
        public float ShakeStrength = 3f;
        public float ShakeDuration = 0.15f;
        public float PunchReturnSpeed = 12f;

        [Header("Rombos indicadores")]
        public float DiamondFollowSpeed = 12f;
        public float DiamondPulseFrequency = 6f;
        public float DiamondPulseAmplitude = 0.15f;

        [Tooltip("Distancia horizontal de cada rombo respecto del borde del label.")]
        public float DiamondMargin = 28f;
        public float DiamondFadeSpeed = 10f;
    }
}
