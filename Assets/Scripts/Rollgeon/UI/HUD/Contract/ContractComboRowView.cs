using System.Collections.Generic;
using Patterns;
using Rollgeon.Combos;
using Rollgeon.Heroes;
using Rollgeon.Localization;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD.Contract
{
    /// <summary>
    /// Una fila de contrato estilo drawer: la mano de ejemplo, el nombre del combo y su
    /// daño base. La columna de descripción es opcional — el drawer in-game la deja sin
    /// cablear (los dados resaltados ya dicen cómo se arma) y la variante de selección de
    /// clase la muestra.
    /// </summary>
    /// <remarks>
    /// Sigue siendo una fila distinta a <see cref="ComboRowView"/>: aquella es la tabla
    /// legacy de solo texto. Desde el rework de selección de clase, esta fila se usa en
    /// ambas pantallas vía prefabs distintos (<c>ContractComboRow</c> sin descripción,
    /// <c>ClassSelectContractRow</c> con ella); la regla compartida sigue siendo
    /// <c>ComboRowView.ResolveBaseDamage</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Contract Combo Row View")]
    public class ContractComboRowView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private RectTransform _diceContainer;
        [SerializeField, Required] private ContractDieView _diePrefab;
        [SerializeField, Required] private TextMeshProUGUI _nameLabel;
        [SerializeField, Required] private TextMeshProUGUI _damageLabel;

        [SerializeField]
        [Tooltip("TMP opcional de la descripción del combo. El drawer in-game lo deja null.")]
        private TextMeshProUGUI _descriptionLabel;

        private readonly List<ContractDieView> _dice = new();

        private BaseComboSO _combo;
        private ContractSheet _sheet;

        public void Bind(BaseComboSO combo, ContractSheet sheet, ContractSheetUiSettingsSO settings)
        {
            if (combo == null) return;
            _combo = combo;
            _sheet = sheet;

            if (_nameLabel != null)
                _nameLabel.text = LocalizedContent.Name(combo.ComboId, combo.DisplayName ?? string.Empty);

            if (_damageLabel != null)
                _damageLabel.text = ComboRowView.ResolveBaseDamage(combo, sheet).ToString();

            if (_descriptionLabel != null)
                _descriptionLabel.text = LocalizedContent.Description(combo.ComboId, combo.Description ?? string.Empty);

            BindDice(combo.ComboId, settings);
        }

        /// <summary>
        /// Re-lee el daño efectivo (capa de modificadores del Boss 3 incluida) y repinta el
        /// label. Lo invoca <see cref="ContractDisplayView"/> al recibir
        /// <see cref="EventName.OnContractModifierChanged"/>.
        /// </summary>
        public void RefreshDamage()
        {
            if (_combo == null || _damageLabel == null) return;

            int baseDmg = ComboRowView.ResolveBaseDamage(_combo, _sheet);
            int effective = baseDmg;
            if (ServiceLocator.TryGetService<Rollgeon.Combat.ContractMod.IContractModifierService>(out var mods)
                && mods != null)
                effective = mods.GetEffectiveBaseDamage(_combo.ComboId, baseDmg);

            _damageLabel.text = effective.ToString();
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
