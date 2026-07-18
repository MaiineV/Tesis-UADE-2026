using PrimeTween;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Tuning del HUD de pilas de fichas (vida/escudo, energía, oro, poción):
    /// sprites, layout, animaciones y SFX. El installer
    /// <c>Rollgeon → Chip Stack HUD</c> crea el asset y asigna los slices.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Chip Stack Settings", fileName = "ChipStackSettings")]
    public class ChipStackSettingsSO : ScriptableObject
    {
        [Header("Sprites (slices — los asigna el installer)")]
        [Tooltip("UI-Sheet-sheet_4 — una ficha por punto de vida.")]
        public Sprite HealthChip;

        [Tooltip("UI-Sheet-sheet_9 — fichas de escudo, van ENCIMA de las de vida.")]
        public Sprite ShieldChip;

        [Tooltip("UI-Sheet-sheet_5 — una ficha por punto de energía.")]
        public Sprite EnergyChip;

        [Tooltip("CoinStyle1.1_0 — ficha plana de la pila de oro (hasta 4).")]
        public Sprite GoldChipFlat;

        [Tooltip("CoinStyle1.1_1 — ficha inclinada 'apoyada' en la pila (oro ≥5). Ya viene girada.")]
        public Sprite GoldChipTilted;

        [Tooltip("PotionSheet_0 — ícono de poción con al menos una disponible.")]
        public Sprite PotionFull;

        [Tooltip("PotionSheet_1 — ícono de poción sin cargas.")]
        public Sprite PotionEmpty;

        [Header("Layout")]
        [Tooltip("Escala de las fichas (pixel-art: enteros). Se aplica al sizeDelta, no al localScale.")]
        public float ChipScale = 1f;

        [Tooltip("Separación vertical (px) entre fichas apiladas de vida/energía.")]
        public float ChipSpacingY = 14f;

        [Tooltip("Separación vertical (px) de la pila de oro (las monedas son más finas).")]
        public float GoldChipSpacingY = 8f;

        [Header("Animación — ganar (ficha cae en la pila)")]
        [Tooltip("Altura (px) desde donde cae la ficha nueva.")]
        public float DropHeight = 60f;

        public float DropDuration = 0.28f;
        public Ease DropEase = Ease.OutBounce;

        [Header("Animación — perder (fade hacia la izquierda)")]
        [Tooltip("Distancia (px) que desliza a la izquierda mientras desvanece.")]
        public float LoseSlideDistance = 40f;

        public float LoseDuration = 0.18f;
        public Ease LoseEase = Ease.OutQuad;

        [Header("Animación — común")]
        [Tooltip("Delay incremental entre fichas cuando cambian varias en un mismo evento.")]
        public float StaggerPerChip = 0.05f;

        [Header("Shake (oro ≥5)")]
        public float ShakeDuration = 0.25f;

        [Tooltip("Amplitud horizontal (px) del shake de la pila de oro.")]
        public float ShakeStrengthX = 6f;

        public float ShakeFrequency = 20f;

        [Header("Colores")]
        [Tooltip("Hex (sin #) del numerador de vida cuando hay escudo. Paleta: Escudo #A3B3B1.")]
        public string ShieldHex = "A3B3B1";

        [Header("SFX (opcionales — null = silencio, IAudioService ignora clips null)")]
        public AudioClip ChipDropClip;
        public AudioClip ChipSpendClip;
        public AudioClip GoldShakeClip;

        [Range(0f, 1f)] public float SfxVolume = 1f;

        [Tooltip("Rango de pitch aleatorio por ficha para que las ráfagas no suenen a metralleta.")]
        public Vector2 SfxPitchRange = new Vector2(0.95f, 1.05f);

        [Tooltip("Máximo de sonidos por ráfaga de cambio (el resto de las fichas caen mudas).")]
        public int MaxSfxPerBatch = 4;
    }
}
