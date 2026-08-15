using System.Collections.Generic;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Una fila del contrato in-game: la mano de ejemplo, el nombre del combo, lo que paga
    /// hoy, y la marca de la regla que el jefe le puso encima. Sin columna de explicación —
    /// los dados resaltados ya dicen cómo se arma.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Es una fila distinta a <see cref="ComboRowView"/> a propósito: aquella vive en la
    /// selección de clase, con otro diseño y otra data (incluye descripción). Compartir un
    /// solo componente ataba dos pantallas que van a evolucionar por separado; lo que sí se
    /// reusa es <c>ComboRowView.ResolveBaseDamage</c>, que es la regla, no el layout.
    /// </para>
    /// <para>
    /// El daño que muestra es el EFECTIVO, no el de la hoja: mientras el Anotador tenga la
    /// fila corrida, el número de la tabla tiene que ser el que va a cobrar el golpe.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Combo Row View")]
    public class ContractComboRowView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _diceContainer;
        [SerializeField, Required] private ContractDieView _diePrefab;
        [SerializeField, Required] private TextMeshProUGUI _nameLabel;
        [SerializeField, Required] private TextMeshProUGUI _damageLabel;

        [Title("Reglas del jefe")]
        [SerializeField]
        [Tooltip("Tachadura sobre el nombre y el daño. Se prende en prohibido, bloqueado y corrido.")]
        private Image _strike;

        [SerializeField]
        [Tooltip("Raíz del badge de regla. Se apaga cuando la fila no tiene marca.")]
        private GameObject _badge;

        [SerializeField]
        [Tooltip("Label del badge: PROHIBIDO / BLOQUEADO N / PAGA COMO <combo> / +N / -N.")]
        private TextMeshProUGUI _badgeLabel;

        [SerializeField, Tooltip("Color de la marca cuando la regla te favorece.")]
        private Color _favorableColor = new Color(0.4f, 1f, 0.45f, 1f);

        [SerializeField, Tooltip("Color de la marca cuando la regla te castiga.")]
        private Color _adverseColor = new Color(1f, 0.45f, 0.4f, 1f);

        private readonly List<ContractDieView> _dice = new();

        // Los colores autorados en el prefab son el estado "sin regla"; se capturan en el
        // primer Bind porque después los pisamos al teñir y ya no hay a dónde volver.
        private Color _nameDefaultColor = Color.white;
        private Color _damageDefaultColor = Color.white;
        private bool _defaultColorsCached;

        /// <summary>
        /// Popula la fila resolviendo ella misma la marca. Para los callers que no tienen la
        /// tabla entera; sin vecinos, un corrimiento se lee como buff o nerf.
        /// </summary>
        public void Bind(BaseComboSO combo, ContractSheet sheet, ContractSheetUiSettingsSO settings)
            => Bind(combo, settings, ContractRowStateResolver.ResolveSingle(combo, sheet));

        /// <summary>
        /// Popula la fila con un estado ya resuelto sobre la tabla completa — es el camino
        /// que usan el drawer y la planilla, y el único que puede decir "paga como aquella".
        /// </summary>
        public void Bind(BaseComboSO combo, ContractSheetUiSettingsSO settings, ContractRowState state)
        {
            if (combo == null) return;

            CacheDefaultColors();

            if (_nameLabel != null)
                _nameLabel.text = LocalizedContent.Name(combo.ComboId, combo.DisplayName ?? string.Empty);

            if (_damageLabel != null)
                _damageLabel.text = state.EffectiveDamage.ToString();

            ApplyRuleMark(state);
            BindDice(combo.ComboId, settings);
        }

        private void CacheDefaultColors()
        {
            if (_defaultColorsCached) return;
            if (_nameLabel != null) _nameDefaultColor = _nameLabel.color;
            if (_damageLabel != null) _damageDefaultColor = _damageLabel.color;
            _defaultColorsCached = true;
        }

        private void ApplyRuleMark(ContractRowState state)
        {
            var tint = state.IsFavorable ? _favorableColor : _adverseColor;

            if (_strike != null)
            {
                _strike.enabled = state.IsStruckThrough;
                _strike.color = tint;
            }

            string badgeText = state.BadgeText();
            bool showBadge = state.IsAltered && !string.IsNullOrEmpty(badgeText);

            if (_badgeLabel != null)
            {
                _badgeLabel.text = badgeText;
                _badgeLabel.color = tint;
            }
            if (_badge != null) _badge.SetActive(showBadge);

            if (_damageLabel != null)
                _damageLabel.color = state.IsAltered ? tint : _damageDefaultColor;

            // El nombre sólo se tiñe cuando la fila está tachada: en un buff o un nerf el
            // número ya cambió de color y teñir todo convertía la tabla en un semáforo.
            if (_nameLabel != null)
                _nameLabel.color = state.IsStruckThrough ? tint : _nameDefaultColor;
        }

        private void BindDice(string comboId, ContractSheetUiSettingsSO settings)
        {
            if (_diceContainer == null || _diePrefab == null || settings == null) return;

            var hand = settings.FindHand(comboId);
            int count = hand?.Count ?? 0;
            EnsureDice(count);

            for (int i = 0; i < _dice.Count; i++)
            {
                bool used = i < count;
                _dice[i].gameObject.SetActive(used);
                if (!used) continue;

                _dice[i].Show(
                    settings.FaceSprite(hand.FaceAt(i)),
                    settings.HighlightFrame,
                    hand.IsHighlighted(i),
                    settings.DimmedAlpha);
            }
        }

        // Los dados se reusan: la fila se re-bindea al abrir el drawer y en cada cambio de
        // contrato, y no vale la pena instanciar cinco Images cada vez.
        private void EnsureDice(int needed)
        {
            while (_dice.Count < needed)
                _dice.Add(Instantiate(_diePrefab, _diceContainer));
        }
    }
}
