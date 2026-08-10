using System.Collections.Generic;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Una fila del contrato in-game: la mano de ejemplo, el nombre del combo y su daño
    /// base. Sin columna de explicación — los dados resaltados ya dicen cómo se arma.
    /// </summary>
    /// <remarks>
    /// Es una fila distinta a <see cref="ComboRowView"/> a propósito: aquella vive en la
    /// selección de clase, con otro diseño y otra data (incluye descripción). Compartir un
    /// solo componente ataba dos pantallas que van a evolucionar por separado; lo que sí se
    /// reusa es <c>ComboRowView.ResolveBaseDamage</c>, que es la regla, no el layout.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Combo Row View")]
    public class ContractComboRowView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _diceContainer;
        [SerializeField, Required] private ContractDieView _diePrefab;
        [SerializeField, Required] private TextMeshProUGUI _nameLabel;
        [SerializeField, Required] private TextMeshProUGUI _damageLabel;

        private readonly List<ContractDieView> _dice = new();

        public void Bind(BaseComboSO combo, ContractSheet sheet, ContractSheetUiSettingsSO settings)
        {
            if (combo == null) return;

            if (_nameLabel != null)
                _nameLabel.text = LocalizedContent.Name(combo.ComboId, combo.DisplayName ?? string.Empty);

            if (_damageLabel != null)
                _damageLabel.text = ComboRowView.ResolveBaseDamage(combo, sheet).ToString();

            BindDice(combo.ComboId, settings);
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
