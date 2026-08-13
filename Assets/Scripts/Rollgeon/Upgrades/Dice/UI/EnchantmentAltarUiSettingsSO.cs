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
    }
}
