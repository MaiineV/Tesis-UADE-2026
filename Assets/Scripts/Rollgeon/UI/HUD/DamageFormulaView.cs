using System;
using Patterns;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    [AddComponentMenu("Rollgeon/UI/HUD/Damage Formula View")]
    public class DamageFormulaView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _formulaLabel;

        private Guid _playerGuid;
        private HeroActionBehavior _currentBehavior;
        private string _lastComboDisplayName;
        private int _lastComboBaseDamage;
        private Action<ComboMatchedPayload> _onComboMatched;

        public void Bind(Guid playerGuid)
        {
            _playerGuid = playerGuid;
            _onComboMatched = OnComboMatched;
            TypedEvent<ComboMatchedPayload>.Subscribe(_onComboMatched);
            ClearFormula();
        }

        public void Unbind()
        {
            if (_onComboMatched != null)
            {
                TypedEvent<ComboMatchedPayload>.Unsubscribe(_onComboMatched);
                _onComboMatched = null;
            }
            _currentBehavior = null;
            ClearFormula();
        }

        public void SetBehavior(HeroActionBehavior behavior)
        {
            _currentBehavior = behavior;
            Debug.Log($"[DamageFormulaView] SetBehavior — '{behavior?.ActionName ?? "null"}'");
            UpdateFormula();
        }

        public void ClearBehavior()
        {
            Debug.Log("[DamageFormulaView] ClearBehavior");
            _currentBehavior = null;
            _lastComboDisplayName = null;
            _lastComboBaseDamage = 0;
            ClearFormula();
        }

        private void OnComboMatched(ComboMatchedPayload payload)
        {
            if (payload.SourceGuid != _playerGuid)
            {
                Debug.Log($"[DamageFormulaView] OnComboMatched — SKIPPED: guid mismatch payload={payload.SourceGuid} mine={_playerGuid}");
                return;
            }
            Debug.Log($"[DamageFormulaView] OnComboMatched — displayName={payload.DisplayName} baseDmg={payload.BaseDamage} _currentBehavior={_currentBehavior?.ActionName ?? "null"}");
            _lastComboDisplayName = payload.DisplayName;
            _lastComboBaseDamage = payload.BaseDamage;
            UpdateFormula();
        }

        private void UpdateFormula()
        {
            if (_formulaLabel == null)
            {
                Debug.Log("[DamageFormulaView] UpdateFormula — _formulaLabel is null, aborting");
                return;
            }
            if (_currentBehavior == null) { ClearFormula(); return; }

            var dmgEff = _currentBehavior.FindFirstDealDamageEffect();
            if (dmgEff == null)
            {
                Debug.Log($"[DamageFormulaView] UpdateFormula — FindFirstDealDamageEffect null for '{_currentBehavior.ActionName}'");
                ClearFormula();
                return;
            }

            if (dmgEff.Source == DamageSource.Constant)
            {
                _formulaLabel.text = $"{_currentBehavior.ActionName} ({dmgEff.BaseAmount})";
                Debug.Log($"[DamageFormulaView] UpdateFormula — Constant → \"{_formulaLabel.text}\"");
                return;
            }

            if (dmgEff.Source == DamageSource.FromReader)
            {
                _formulaLabel.text = $"{_currentBehavior.ActionName} (stat)";
                Debug.Log($"[DamageFormulaView] UpdateFormula — FromReader → \"{_formulaLabel.text}\"");
                return;
            }

            if (_lastComboBaseDamage <= 0)
            {
                _formulaLabel.text = $"{_currentBehavior.ActionName} (sin combo)";
                Debug.Log($"[DamageFormulaView] UpdateFormula — sin combo → \"{_formulaLabel.text}\"");
                return;
            }

            string comboName = !string.IsNullOrEmpty(_lastComboDisplayName) ? _lastComboDisplayName : "Combo";
            int total = Mathf.RoundToInt(_lastComboBaseDamage * dmgEff.ComboMultiplier);
            _formulaLabel.text = $"{comboName} ({_lastComboBaseDamage}) \u00d7 {_currentBehavior.ActionName} ({dmgEff.ComboMultiplier}) = {total}";
            Debug.Log($"[DamageFormulaView] UpdateFormula → \"{_formulaLabel.text}\"");
        }

        private void ClearFormula()
        {
            if (_formulaLabel != null) _formulaLabel.text = string.Empty;
        }
    }
}
