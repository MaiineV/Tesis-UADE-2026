using PrimeTween;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Tuning de la UI de armado de bolsa (<see cref="BuildSelectionScreen"/>):
    /// sprites estáticos por tipo de dado (slices elegidos a mano del sheet
    /// <c>Art/UI/Dices</c> — NO el DiceShapeCatalog), layout de la tira y
    /// animaciones/SFX. El installer <c>Rollgeon → Build Selection</c> crea el
    /// asset y asigna los sprites.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Dice Build UI Settings", fileName = "DiceBuildUiSettings")]
    public class DiceBuildUiSettingsSO : ScriptableObject
    {
        [Header("Sprites por tipo (los asigna el installer)")]
        public Sprite D4;   // Dices_2
        public Sprite D6;   // Dices_6
        public Sprite D8;   // Dices_11
        public Sprite D10;  // Dices_16
        public Sprite D12;  // Dices_21
        public Sprite D20;  // Dices_26

        [Header("Tira de dados")]
        [Tooltip("Tamaño (px) de cada dado en la tira.")]
        public float DieSize = 96f;

        [Tooltip("Separación (px) entre centros de dados.")]
        public float Spacing = 118f;

        [Header("Animación — agregar (cae en la tira)")]
        public float DropHeight = 70f;
        public float DropDuration = 0.3f;
        public Ease DropEase = Ease.OutBack;

        [Header("Animación — quitar")]
        public float RemoveDuration = 0.18f;
        public Ease RemoveEase = Ease.InBack;

        [Header("Animación — reacomodo y limpiar")]
        [Tooltip("Duración del slide de los vecinos a su nueva posición.")]
        public float SlideDuration = 0.22f;
        public Ease SlideEase = Ease.OutCubic;

        [Tooltip("Delay incremental al limpiar toda la tira.")]
        public float ClearStagger = 0.05f;

        [Header("Juice del pool")]
        [Tooltip("Punch de escala del dado del pool al clickearlo.")]
        public float PoolPunchScale = 0.15f;
        public float PoolPunchDuration = 0.2f;

        [Header("Juice de la tira")]
        [Tooltip("Rotación Z (grados) con la que el dado cae antes de enderezarse.")]
        public float DropRotation = 16f;

        [Tooltip("ScaleY del squash al aterrizar (1 = sin squash).")]
        public float LandSquashScale = 0.82f;
        public float LandSquashDuration = 0.07f;

        [Tooltip("Escala del dado de la tira bajo el mouse (hover clickeable).")]
        public float HoverScale = 1.12f;
        public float HoverDuration = 0.12f;

        [Header("Partículas de impacto (mini burst)")]
        [Tooltip("Cuadraditos por burst al agregar/quitar. 0 = sin partículas.")]
        public int BurstCount = 8;
        public float BurstDistanceMin = 28f;
        public float BurstDistanceMax = 60f;
        public float BurstDuration = 0.35f;
        public float BurstParticleSize = 6f;
        public Color AddBurstColor = new Color32(0xE0, 0xC0, 0xA9, 0xFF);
        public Color RemoveBurstColor = new Color32(0x8A, 0x96, 0xA0, 0xFF);

        [Header("Bolsa completa (ola)")]
        [Tooltip("Salto (px) de cada dado en la ola al completar la bolsa.")]
        public float WaveJumpHeight = 16f;
        public float WaveJumpDuration = 0.14f;
        public float WaveStagger = 0.06f;

        [Header("SFX (opcionales — null = silencio)")]
        public AudioClip AddClip;
        public AudioClip RemoveClip;
        public AudioClip ClearClip;

        [Range(0f, 1f)] public float SfxVolume = 1f;
        public Vector2 SfxPitchRange = new Vector2(0.95f, 1.05f);

        public Sprite GetSprite(DiceType type)
        {
            switch (type)
            {
                case DiceType.D4: return D4;
                case DiceType.D6: return D6;
                case DiceType.D8: return D8;
                case DiceType.D10: return D10;
                case DiceType.D12: return D12;
                case DiceType.D20: return D20;
                // D3 (tutorial) no tiene slice propio — reusa el del D4.
                case DiceType.D3: return D4;
                default: return null;
            }
        }
    }
}
