using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Sub-view que representa una fila de la tabla de contratos: nombre + daño base +
    /// descripcion + icono opcional. Se instancia desde <see cref="ContractDisplayView.Bind"/>.
    /// Plan §4.4.
    /// </summary>
    /// <remarks>
    /// [SETUP] El prefab vive en <c>Assets/Rollgeon/Prefabs/UI/ComboRow.prefab</c>, armado por
    /// el usuario en engine (instructivo §8.5). Root con <c>HorizontalLayoutGroup</c> + TMP
    /// labels + Image icon.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Combo Row View")]
    public class ComboRowView : MonoBehaviour
    {
        [Title("Combo Row — Widget refs")]
        [Required("Arrastrar el TMP del nombre del combo.")]
        [SerializeField]
        private TextMeshProUGUI _nameLabel;

        [Required("Arrastrar el TMP del dano base.")]
        [SerializeField]
        private TextMeshProUGUI _damageLabel;

        [SerializeField]
        [Tooltip("TMP opcional de la descripcion. Null si no aplica.")]
        private TextMeshProUGUI _descriptionLabel;

        [SerializeField]
        [Tooltip("Image opcional del icono del combo. Si combo.Icon es null, se desactiva.")]
        private Image _iconImage;

        private static readonly Color BuffedColor = new Color(0.4f, 1f, 0.45f, 1f);
        private static readonly Color NerfedColor = new Color(1f, 0.45f, 0.4f, 1f);

        private BaseComboSO _combo;
        private ContractSheet _sheetOverride;
        private Color _damageDefaultColor = Color.white;
        private bool _damageDefaultColorCached;

        /// <summary>
        /// Popula la fila con los datos de <paramref name="combo"/>: <see cref="BaseComboSO.DisplayName"/>,
        /// daño base efectivo (tras la capa de modificadores del Contrato — Boss 3 §4),
        /// <see cref="BaseComboSO.Description"/> (opcional) e <see cref="BaseComboSO.Icon"/> (opcional).
        /// Fallback a <see cref="BaseComboSO.ComboId"/> si <c>DisplayName</c> es null/empty.
        /// </summary>
        public void Bind(BaseComboSO combo) => Bind(combo, sheetOverride: null);

        /// <summary>
        /// Variante con <paramref name="sheetOverride"/>: en selección de clase no hay
        /// <c>IPlayerService.CurrentHero</c> todavía y sin el override la fila mostraría
        /// el daño del catálogo global en vez del de la clase elegida.
        /// </summary>
        public void Bind(BaseComboSO combo, ContractSheet sheetOverride)
        {
            if (combo == null) return;
            _combo = combo;
            _sheetOverride = sheetOverride;

            if (_nameLabel != null)
            {
                var name = combo.DisplayName;
                var fallback = string.IsNullOrEmpty(name) ? (combo.ComboId ?? string.Empty) : name;
                _nameLabel.text = LocalizedContent.Name(combo.ComboId, fallback);
            }

            RefreshDamage();

            if (_descriptionLabel != null)
            {
                _descriptionLabel.text = LocalizedContent.Description(combo.ComboId, combo.Description ?? string.Empty);
            }

            if (_iconImage != null)
            {
                if (combo.Icon != null)
                {
                    _iconImage.sprite = combo.Icon;
                    _iconImage.enabled = true;
                }
                else
                {
                    _iconImage.enabled = false;
                }
            }
        }

        /// <summary>
        /// Re-lee el daño efectivo del combo desde <see cref="ContractMod.IContractModifierService"/>
        /// y repinta el label. Lo invoca <see cref="ContractDisplayView"/> al recibir
        /// <see cref="EventName.OnContractModifierChanged"/> — refleja las Reglas del Boss 3 en vivo.
        /// </summary>
        public void RefreshDamage()
        {
            if (_combo == null || _damageLabel == null) return;

            if (!_damageDefaultColorCached)
            {
                _damageDefaultColor = _damageLabel.color;
                _damageDefaultColorCached = true;
            }

            // Base plano de la tabla por clase (Spec Daño v2) — la lista del contrato debe
            // mostrar los valores de ESTA clase, no los del catálogo global.
            int baseDmg = ResolveBaseDamage(_combo, _sheetOverride);

            int effective = baseDmg;
            if (ServiceLocator.TryGetService<Rollgeon.Combat.ContractMod.IContractModifierService>(out var mods) && mods != null)
                effective = mods.GetEffectiveBaseDamage(_combo.ComboId, baseDmg);

            _damageLabel.text = effective.ToString();
            _damageLabel.color = effective > baseDmg ? BuffedColor
                : effective < baseDmg ? NerfedColor
                : _damageDefaultColor;
        }

        /// <summary>
        /// Resolución del daño base de la fila, pura para tests: override explícito
        /// (selección de clase) → sheet del héroe actual → base del catálogo.
        /// </summary>
        public static int ResolveBaseDamage(BaseComboSO combo, ContractSheet sheetOverride)
        {
            if (combo == null) return 0;

            if (sheetOverride != null) return sheetOverride.GetBaseDamage(combo);

            if (ServiceLocator.TryGetService<Rollgeon.Player.IPlayerService>(out var player)
                && player?.CurrentHero?.Sheet != null)
                return player.CurrentHero.Sheet.GetBaseDamage(combo);

            return combo.BaseDamage;
        }
    }
}
