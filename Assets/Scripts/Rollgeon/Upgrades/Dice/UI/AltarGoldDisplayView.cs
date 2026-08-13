using PrimeTween;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Upgrades.Dice.UI
{
    /// <summary>
    /// Pila de oro del Altar de Encantamiento: reutiliza <see cref="ChipStackView"/>
    /// y los sprites del HUD, pero SIEMPRE muestra la pila completa (4 fichas
    /// planas + la inclinada) sin importar el oro real — acá la pila es icono,
    /// no medidor; la cantidad exacta la dice el label.
    /// </summary>
    /// <remarks>
    /// No se suscribe a <c>OnGoldChanged</c>: <see cref="EnchantmentAltarView"/>
    /// ya escucha ese evento y llama <see cref="Refresh"/> — un solo dueño del
    /// flujo de datos dentro del panel.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Upgrades/Dice/UI/Altar Gold Display View")]
    public sealed class AltarGoldDisplayView : MonoBehaviour
    {
        private static readonly int[] FullStack = { 0, 0, 0, 0 };

        [Title("Gold Display — Widget refs")]
        [Required, SerializeField] private ChipStackView _stack;
        [Required, SerializeField] private Image _tiltedChip;
        [Required, SerializeField] private TextMeshProUGUI _label;
        [Required, SerializeField] private ChipStackSettingsSO _settings;

        private bool _configured;

        /// <summary>Label de cantidad — <c>EnchantmentAltarView</c> lo usa como su gold label.</summary>
        public TextMeshProUGUI Label => _label;

        private void Awake() => EnsureConfigured();

        private void EnsureConfigured()
        {
            if (_configured || _stack == null || _settings == null) return;
            _stack.Configure(_settings, new[] { _settings.GoldChipFlat }, _settings.GoldChipSpacingY);

            if (_tiltedChip != null && _settings.GoldChipTilted != null)
            {
                _tiltedChip.sprite = _settings.GoldChipTilted;
                ((RectTransform)_tiltedChip.transform).sizeDelta =
                    _settings.GoldChipTilted.rect.size * Mathf.Max(1f, _settings.ChipScale);
                _tiltedChip.raycastTarget = false;
            }
            _configured = true;
        }

        /// <summary>
        /// Garantiza la pila completa. Con <paramref name="animate"/> las fichas
        /// hacen su drop-in staggered la primera vez (los refresh siguientes no
        /// re-animan: el modelo de la pila ya está en 4). El texto del label lo
        /// escribe <c>EnchantmentAltarView</c> — dueña del dato de oro.
        /// </summary>
        public void Refresh(bool animate)
        {
            EnsureConfigured();
            if (_stack != null) _stack.SetChips(FullStack, animate);
            if (_tiltedChip != null) _tiltedChip.gameObject.SetActive(true);
        }

        /// <summary>Shake de la pila — feedback al gastar oro en un encantamiento.</summary>
        public void Shake()
        {
            if (_stack != null) _stack.Shake();
        }

        private void OnDisable()
        {
            // ChipStackView limpia sus propios tweens; acá solo el nuestro (ninguno
            // por ahora) — el StopAll defensivo evita fugas si se agrega juice local.
            Tween.StopAll(onTarget: this);
        }
    }
}
