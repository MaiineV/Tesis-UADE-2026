using TMPro;
using UnityEngine;

namespace Rollgeon.Tutorial.UI
{
    /// <summary>
    /// Config visual del <see cref="TutorialOverlay"/> — mismo rol que
    /// <c>LoadingScreenSettingsSO</c> para el loading screen. Crear el asset,
    /// asignar material dim / sprite de flecha / fuente, y referenciarlo desde
    /// el <see cref="TutorialOverlayBootstrap"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Tutorial/Tutorial Overlay Settings", fileName = "TutorialOverlaySettings")]
    public sealed class TutorialOverlaySettingsSO : ScriptableObject
    {
        [Header("Canvas")]
        [Tooltip("Arriba del HUD (default) y debajo de PersistentUiOverlay (30000) / LoadingScreen.")]
        public int SortingOrder = 29000;
        public Vector2 ReferenceResolution = new Vector2(1920, 1080);
        [Range(0f, 1f)] public float MatchWidthOrHeight = 0.5f;

        [Header("Dim + recorte")]
        [Tooltip("Material con el shader Rollgeon/UI/TutorialDim. El overlay lo INSTANCIA " +
                 "(no muta el asset).")]
        public Material DimMaterial;
        [Tooltip("Color del opacado (alpha = intensidad).")]
        public Color DimColor = new Color(0f, 0f, 0f, 0.78f);
        [Tooltip("Radio default del recorte para anchors de mundo, en píxeles de pantalla.")]
        public float DefaultCutoutRadiusPx = 130f;
        public float CutoutFeatherPx = 26f;
        [Tooltip("Padding sumado al radio auto-derivado de un RectTransform del HUD.")]
        public float UiCutoutPaddingPx = 18f;
        public float ShowDuration = 0.25f;
        public float HideDuration = 0.18f;

        [Header("Flecha")]
        [Tooltip("Sprite que apunta a la DERECHA en rotación 0.")]
        public Sprite ArrowSprite;
        public Vector2 ArrowSize = new Vector2(64f, 64f);
        [Tooltip("Separación entre la punta de la flecha y el borde del recorte, en píxeles.")]
        public float ArrowGapPx = 14f;
        [Tooltip("Amplitud del vaivén idle de la flecha (px). 0 = sin animación.")]
        public float ArrowBobAmplitudePx = 10f;
        public float ArrowBobDuration = 0.6f;

        [Header("Popup")]
        public Sprite PopupBackgroundSprite;
        [Tooltip("Multiplicador de pixels-per-unit del 9-slice: <1 engrosa el borde " +
                 "del frame en pantalla (0.5 lo duplica).")]
        public float PopupBackgroundPpuMultiplier = 1f;
        public Color PopupBackgroundColor = new Color(0.08f, 0.07f, 0.12f, 0.95f);
        public TMP_FontAsset PopupFont;
        public float PopupFontSize = 30f;
        public Color PopupTextColor = Color.white;
        public float PopupMaxWidth = 520f;
        public Vector2 PopupPadding = new Vector2(28f, 22f);
        [Tooltip("Alpha del footer 'clic para continuar'. 0 (asset viejo sin el campo) " +
                 "cae al default en código.")]
        [Range(0f, 1f)] public float PopupFooterAlpha = 0.9f;
        [Tooltip("Margen mínimo del popup contra los bordes de pantalla (px).")]
        public float PopupScreenMargin = 40f;
        [Tooltip("Separación entre el borde del anchor y el popup adyacente, en px " +
                 "de pantalla — deja lugar a la flecha.")]
        public float PopupAnchorGapPx = 90f;
        [Tooltip("Rollback de emergencia: true vuelve al layout viejo (popup en el " +
                 "cuadrante opuesto al anchor) en vez del adyacente.")]
        public bool LegacyQuadrantPlacement;
        [Tooltip("Histéresis del flip de cuadrante como fracción de pantalla — evita " +
                 "que el popup salte de lado con anchors en movimiento.")]
        [Range(0f, 0.25f)] public float QuadrantHysteresis = 0.08f;

        [Header("Paso de texto puro (BlockUntilContinue)")]
        public string ContinueFooterText = "Haz clic para continuar";
    }
}
