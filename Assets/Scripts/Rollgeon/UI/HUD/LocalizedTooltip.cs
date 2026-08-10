using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// Tooltip de texto fijo y localizado para un elemento de UI. Lo usan los íconos del
    /// cluster (contrato, mochila, bolsa de dados), que no tienen datos que mostrar más
    /// allá de decir qué son.
    /// </summary>
    /// <remarks>
    /// No se suscribe a <c>LocalizationRefresh</c> a propósito: el
    /// <see cref="UITooltipTrigger"/> llama al provider en CADA hover, así que el texto se
    /// resuelve fresco y un cambio de idioma con el tooltip cerrado ya queda contemplado.
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/HUD/Localized Tooltip")]
    [RequireComponent(typeof(UITooltipTrigger))]
    public class LocalizedTooltip : MonoBehaviour
    {
        [SerializeField, Required]
        [Tooltip("Key de la tabla UI.")]
        private string _key;

        [SerializeField, TextArea(2, 4)]
        [Tooltip("Texto si la key no está en la tabla o Localization todavía no resolvió.")]
        private string _fallback;

        private UITooltipTrigger _trigger;

        private void Awake()
        {
            if (_trigger == null) TryGetComponent(out _trigger);
            if (_trigger != null) _trigger.TextProvider = Resolve;
        }

        private string Resolve()
            => string.IsNullOrEmpty(_key) ? _fallback : LocalizedContent.Ui(_key, _fallback);

        /// <summary>Autoría por código — lo usa el installer.</summary>
        public void Configure(string key, string fallback)
        {
            _key = key;
            _fallback = fallback;
        }
    }
}
