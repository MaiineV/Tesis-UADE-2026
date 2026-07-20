using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Prompt central del tablero para la entrada PAGA a una fase de chain (sin rolls
    /// sobrantes del pool anterior pero con energía): "Shield Roll (1E)". Lo muestra y
    /// esconde <c>CombatHandoffService</c> vía <c>CombatHUDView.Show/HideChainRollPrompt</c>.
    /// Strings serializados sin localización — mismo criterio que <see cref="RerollCountView"/>.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Chain Roll Prompt View")]
    public sealed class ChainRollPromptView : MonoBehaviour
    {
        [SerializeField, Optional, Tooltip("Label del prompt. Sin ref, solo se activa/desactiva el GO.")]
        private TextMeshProUGUI _label;

        [SerializeField, Tooltip("Formato del prompt. {0} = Label de la ChainPhase.")]
        private string _format = "{0} Roll (1E)";

        [SerializeField, Tooltip("Nombre de fase fallback cuando la ChainPhase no tiene Label.")]
        private string _fallbackPhaseLabel = "Phase";

        public void Show(string phaseLabel)
        {
            if (_label != null)
            {
                var name = string.IsNullOrEmpty(phaseLabel) ? _fallbackPhaseLabel : phaseLabel;
                _label.text = string.Format(_format, name);
            }
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
