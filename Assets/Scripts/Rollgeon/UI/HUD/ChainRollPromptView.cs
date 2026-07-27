using Patterns;
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
    /// <remarks>
    /// Además del Show/Hide explícito, el prompt se apaga solo con
    /// <see cref="EventName.OnChainCompleted"/> / <see cref="EventName.OnCombatEnd"/>: los dos
    /// significan "no hay chain en curso", así que un prompt vivo después de cualquiera de
    /// ellos está de más. Es la garantía de que no sobreviva al chain que lo mostró por un
    /// camino de salida que se olvide de esconderlo — que es lo que pasaba cuando el combate
    /// terminaba con la entrada paga pendiente y el prompt reaparecía sobre el roll de ataque
    /// del combate siguiente (PUL-016).
    /// <para>
    /// La suscripción se engancha en <see cref="Show"/> y se suelta en <see cref="Hide"/> (no en
    /// OnEnable/OnDisable) para que la ventana de escucha sea exactamente "el prompt está
    /// arriba", sin depender de los callbacks de lifecycle — que en EditMode no corren.
    /// <c>OnDisable</c> queda solo como guard de un teardown que desactive el GO sin pasar por
    /// <see cref="Hide"/>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Chain Roll Prompt View")]
    public sealed class ChainRollPromptView : MonoBehaviour
    {
        [SerializeField, Optional, Tooltip("Label del prompt. Sin ref, solo se activa/desactiva el GO.")]
        private TextMeshProUGUI _label;

        [SerializeField, Tooltip("Formato del prompt. {0} = Label de la ChainPhase.")]
        private string _format = "{0} Roll (1E)";

        [SerializeField, Tooltip("Nombre de fase fallback cuando la ChainPhase no tiene Label.")]
        private string _fallbackPhaseLabel = "Phase";

        private bool _subscribed;

        public void Show(string phaseLabel)
        {
            if (_label != null)
            {
                var name = string.IsNullOrEmpty(phaseLabel) ? _fallbackPhaseLabel : phaseLabel;
                _label.text = string.Format(_format, name);
            }
            gameObject.SetActive(true);
            Subscribe();
        }

        public void Hide()
        {
            Unsubscribe();
            gameObject.SetActive(false);
        }

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnChainCompleted, HandleChainOver);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleChainOver);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnChainCompleted, HandleChainOver);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleChainOver);
            _subscribed = false;
        }

        private void HandleChainOver(params object[] args) => Hide();
    }
}
