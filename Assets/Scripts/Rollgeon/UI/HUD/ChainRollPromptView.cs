using System;
using Patterns;
using Rollgeon.Localization;
using Rollgeon.UI.Utility;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Prompt central del tablero para la entrada PAGA a una fase de chain (sin rolls
    /// sobrantes del pool anterior pero con energía): "Shield Roll  -1 [icono energía]",
    /// con un subtítulo que explica la regla detrás del número.
    /// Lo muestra y esconde <c>CombatHandoffService</c> vía
    /// <c>CombatHUDView.Show/HideChainRollPrompt</c>.
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

        [SerializeField, Tooltip("Formato del prompt. {0} = Label de la ChainPhase, {ENERGY} = " +
                                 "icono del atlas. Fallback si la tabla UI no tiene la key.")]
        private string _format = "{0} Roll  -1 {ENERGY}";

        [SerializeField, Tooltip("Nombre de fase fallback cuando la ChainPhase no tiene Label.")]
        private string _fallbackPhaseLabel = "Phase";

        [SerializeField, Optional, Tooltip("Subtítulo bajo el prompt que explica el costo. " +
                                           "Sin ref, el prompt muestra solo la línea principal.")]
        private TextMeshProUGUI _hintLabel;

        [SerializeField, Tooltip("Texto del subtítulo. Fallback si la tabla UI no tiene la key.")]
        private string _hintText = "Cada roll adicional cuesta 1 de Energía.";

        [SerializeField, Optional, Tooltip("Botón del prompt (BUG-034). Sin ref, el prompt no es clickeable.")]
        private Button _button;

        [Tooltip("Click sobre el prompt — CombatHUDView lo rutea al mismo entry point que el botón Roll.")]
        public UnityEvent OnPromptClicked = new UnityEvent();

        private bool _subscribed;

        // El label de la fase que pidió el último Show — necesario para repintar el
        // prompt al cambiar de idioma sin que el caller vuelva a llamar a Show.
        private string _currentPhaseLabel;

        private Action _onLanguageChanged;

        public void Show(string phaseLabel)
        {
            _currentPhaseLabel = phaseLabel;
            Render();
            gameObject.SetActive(true);
            Subscribe();
        }

        public void Hide()
        {
            Unsubscribe();
            gameObject.SetActive(false);
        }

        /// <summary>Pinta el prompt desde <see cref="_currentPhaseLabel"/>.</summary>
        /// <remarks>
        /// El hint no tiene visibilidad propia: vive como hijo del GO del prompt, así que
        /// el <c>SetActive</c> de <see cref="Show"/>/<see cref="Hide"/> ya lo prende y apaga.
        /// Es deliberado — el prompt <b>siempre</b> es la entrada paga, de modo que "hay
        /// prompt" y "el costo aplica" son la misma condición y no pueden desincronizarse.
        /// </remarks>
        private void Render()
        {
            if (_hintLabel != null)
                _hintLabel.text = IconSpriteTags.ReplacePlaceholders(
                    LocalizedContent.Ui(UiTextKeys.ChainRollPaidHint, _hintText));

            if (_label == null) return;

            var phase = string.IsNullOrEmpty(_currentPhaseLabel) ? _fallbackPhaseLabel : _currentPhaseLabel;

            // Orden obligatorio: traducir → expandir {ICON} → string.Format.
            // El {ENERGY} tiene que desaparecer ANTES del Format: string.Format lee
            // cualquier {…} como placeholder suyo y tira FormatException al no poder
            // parsear "ENERGY" como índice de argumento.
            var format = LocalizedContent.Ui(UiTextKeys.ChainRollPaid, _format);
            _label.text = string.Format(IconSpriteTags.ReplacePlaceholders(format), phase);
        }

        // El botón solo escucha mientras el prompt está arriba — misma ventana que
        // las suscripciones del bus, y por el mismo motivo (BUG-034: el texto es
        // una affordance de pago; fuera de la entrada paga no debe reaccionar).
        private void HandleButtonClick() => OnPromptClicked?.Invoke();

        private void OnDisable() => Unsubscribe();

        private void Subscribe()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnChainCompleted, HandleChainOver);
            EventManager.Subscribe(EventName.OnCombatEnd, HandleChainOver);
            if (_button != null) _button.onClick.AddListener(HandleButtonClick);
            // El prompt se setea por código, así que el package no lo repinta solo al
            // cambiar de idioma. Misma ventana que el resto: solo mientras está arriba.
            _onLanguageChanged = Render;
            LocalizationRefresh.Subscribe(_onLanguageChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnChainCompleted, HandleChainOver);
            EventManager.UnSubscribe(EventName.OnCombatEnd, HandleChainOver);
            if (_button != null) _button.onClick.RemoveListener(HandleButtonClick);
            if (_onLanguageChanged != null)
            {
                LocalizationRefresh.Unsubscribe(_onLanguageChanged);
                _onLanguageChanged = null;
            }
            _subscribed = false;
        }

        private void HandleChainOver(params object[] args) => Hide();
    }
}
