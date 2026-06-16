using UnityEngine;

namespace Rollgeon.Rendering
{
    /// <summary>
    /// Asset de paleta global con generación automática de sombra y luz desde un color base.
    /// El artista elige un color base; el sistema aplica offsets HSV para generar los tres
    /// tonos del cel shading manteniendo armonía cromática.
    ///
    /// Uso:
    ///   Assets > Create > Rollgeon > Palette Asset
    ///   Asignar al GlobalPaletteManager en la escena.
    ///   En cada material PaletteCelLit activar "Use Palette" y elegir el slot.
    /// </summary>
    [CreateAssetMenu(fileName = "PA_NewPalette", menuName = "Rollgeon/Palette Asset")]
    public class PaletteAsset : ScriptableObject
    {
        public const int MaxSlots = 32;

        // Evento estático: GlobalPaletteManager se suscribe para saber cuándo
        // subir los nuevos colores a la GPU. Se dispara desde OnValidate (editor)
        // y desde InitializeSlots (runtime/reset).
        public static event System.Action<PaletteAsset> OnPaletteChanged;

        // ── Offsets HSV por defecto ──────────────────────────────────────────────
        const float DefaultShadowH = +15f;
        const float DefaultShadowS = +0.10f;
        const float DefaultShadowV = -0.28f;

        const float DefaultLightH  = -10f;
        const float DefaultLightS  = -0.12f;
        const float DefaultLightV  = +0.22f;

        // ── Estructura de slot ───────────────────────────────────────────────────
        [System.Serializable]
        public struct Slot
        {
            [Tooltip("Nombre descriptivo (solo editor)")]
            public string label;

            [Tooltip("Color base — el mid/base shade del cel shading")]
            public Color baseColor;

            [Header("Generación automática HSV")]
            [Tooltip("ON = genera sombra y luz desde el color base.\nOFF = usa los colores manuales de abajo.")]
            public bool autoGenerate;

            [Range(-180f, 180f)] public float shadowHueShift;
            [Range(-1f,   1f)]   public float shadowSatShift;
            [Range(-1f,   1f)]   public float shadowValShift;

            [Range(-180f, 180f)] public float lightHueShift;
            [Range(-1f,   1f)]   public float lightSatShift;
            [Range(-1f,   1f)]   public float lightValShift;

            [Header("Colores manuales (solo si Auto Generar está OFF)")]
            public Color lightColor;
            public Color midColor;
            public Color shadowColor;

            // ── Colores computados (los que van a la GPU) ────────────────────────
            public Color ComputedMid    => autoGenerate ? baseColor : midColor;
            public Color ComputedLight  => autoGenerate ? ShiftHSV(baseColor,  lightHueShift,  lightSatShift,  lightValShift)  : lightColor;
            public Color ComputedShadow => autoGenerate ? ShiftHSV(baseColor, shadowHueShift, shadowSatShift, shadowValShift) : shadowColor;

            static Color ShiftHSV(Color c, float hDeg, float sShift, float vShift)
            {
                Color.RGBToHSV(c, out float h, out float s, out float v);
                h = Mathf.Repeat(h + hDeg / 360f, 1f);
                s = Mathf.Clamp01(s + sShift);
                v = Mathf.Clamp01(v + vShift);
                return Color.HSVToRGB(h, s, v);
            }
        }

        [SerializeField]
        public Slot[] slots = new Slot[MaxSlots];

        // ── Presets (32 slots) ───────────────────────────────────────────────────
        static readonly (string name, Color baseColor)[] Presets =
        {
            // ── Tus materiales actuales (slots 0-15) ────────────────────────────
            ( "Black",       new Color(0.10f, 0.10f, 0.12f) ),
            ( "Bone",        new Color(0.88f, 0.83f, 0.72f) ),
            ( "Brown",       new Color(0.42f, 0.24f, 0.10f) ),
            ( "DarkGray",    new Color(0.25f, 0.25f, 0.28f) ),
            ( "DarkYellow",  new Color(0.55f, 0.46f, 0.08f) ),
            ( "Gold",        new Color(0.82f, 0.67f, 0.08f) ),
            ( "Gray",        new Color(0.50f, 0.50f, 0.53f) ),
            ( "Green",       new Color(0.12f, 0.48f, 0.12f) ),
            ( "LightBrown",  new Color(0.64f, 0.44f, 0.24f) ),
            ( "LightGray",   new Color(0.74f, 0.74f, 0.76f) ),
            ( "LightGreen",  new Color(0.30f, 0.72f, 0.22f) ),
            ( "LightYellow", new Color(0.95f, 0.90f, 0.52f) ),
            ( "Red",         new Color(0.68f, 0.10f, 0.10f) ),
            ( "Red 1",       new Color(0.78f, 0.16f, 0.14f) ),
            ( "White",       new Color(0.94f, 0.94f, 0.94f) ),
            ( "Yellow",      new Color(0.90f, 0.78f, 0.08f) ),

            // ── Placeholders (slots 16-31) ───────────────────────────────────────
            ( "Teal",        new Color(0.08f, 0.55f, 0.50f) ),
            ( "Cyan",        new Color(0.10f, 0.72f, 0.80f) ),
            ( "Purple",      new Color(0.40f, 0.12f, 0.58f) ),
            ( "Pink",        new Color(0.88f, 0.42f, 0.62f) ),
            ( "Orange",      new Color(0.85f, 0.42f, 0.08f) ),
            ( "DarkRed",     new Color(0.42f, 0.05f, 0.05f) ),
            ( "DarkBlue",    new Color(0.08f, 0.12f, 0.42f) ),
            ( "LightBlue",   new Color(0.45f, 0.65f, 0.90f) ),
            ( "Salmon",      new Color(0.90f, 0.52f, 0.42f) ),
            ( "Olive",       new Color(0.38f, 0.42f, 0.08f) ),
            ( "Lavender",    new Color(0.68f, 0.58f, 0.88f) ),
            ( "Mint",        new Color(0.52f, 0.88f, 0.68f) ),
            ( "Coral",       new Color(0.88f, 0.38f, 0.30f) ),
            ( "Navy",        new Color(0.05f, 0.08f, 0.30f) ),
            ( "Peach",       new Color(0.95f, 0.72f, 0.58f) ),
            ( "Charcoal",    new Color(0.18f, 0.18f, 0.20f) ),
        };

        void Reset() => InitializeSlots();

        [ContextMenu("Reset to Default Presets")]
        public void InitializeSlots()
        {
            slots = new Slot[MaxSlots];
            for (int i = 0; i < MaxSlots; i++)
            {
                slots[i] = new Slot
                {
                    label          = Presets[i].name,
                    baseColor      = Presets[i].baseColor,
                    autoGenerate   = true,
                    shadowHueShift = DefaultShadowH,
                    shadowSatShift = DefaultShadowS,
                    shadowValShift = DefaultShadowV,
                    lightHueShift  = DefaultLightH,
                    lightSatShift  = DefaultLightS,
                    lightValShift  = DefaultLightV,
                    lightColor     = Color.white,
                    midColor       = Presets[i].baseColor,
                    shadowColor    = Color.black,
                };
            }
            OnPaletteChanged?.Invoke(this);
        }

#if UNITY_EDITOR
        // Se dispara cada vez que el artista modifica cualquier campo en el Inspector
        void OnValidate() => OnPaletteChanged?.Invoke(this);
#endif
    }
}
