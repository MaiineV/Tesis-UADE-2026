using Rollgeon.UI.HUD.Status;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Una tarjeta de la columna del tooltip: una sola cosa en juego, nunca un párrafo.
    /// Título siempre a la izquierda; el ícono, si hay arte, va delante de él.
    /// </summary>
    /// <remarks>
    /// Igual que <see cref="Rollgeon.UI.HUD.Status.StatusEffectIconView"/>: la columna recicla
    /// slots instanciados inactivos, así que <c>Awake</c> nunca corre en el primer uso — nada
    /// acá puede depender de él (ver <c>StatusEffectIconView.cs:55-57</c>).
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Tooltips/Tooltip Card View")]
    public class TooltipCardView : MonoBehaviour
    {
        [Title("Refs")]
        [SerializeField, Required] private TextMeshProUGUI _titleLabel;
        [SerializeField, Required] private TextMeshProUGUI _ruleLabel;
        [SerializeField, Required] private GameObject _iconRoot;
        [SerializeField, Required] private Image _icon;
        [SerializeField, Required] private GameObject _badge;
        [SerializeField, Required] private TextMeshProUGUI _badgeLabel;

        [Tooltip("Lo que pega, a la derecha del título. Se apaga cuando la intención no pega por " +
                 "sí misma.")]
        [SerializeField] private TextMeshProUGUI _damageLabel;

        [Tooltip("Renglón chico arriba del título — 'Próximo turno'. Se apaga cuando el estado " +
                 "no trae fecha.")]
        [SerializeField] private TextMeshProUGUI _eyebrowLabel;

        [Tooltip("Línea entre el título y la regla. Se apaga con la regla: un divisor sin nada " +
                 "debajo parte la tarjeta en dos por nada.")]
        [SerializeField] private GameObject _divider;

        /// <summary>Id del estado que esta tarjeta está mostrando — la columna lo usa para reusarla.</summary>
        public string CardId { get; private set; }

        public void Show(in StatusIconState state)
        {
            CardId = state.Id;
            name = string.IsNullOrEmpty(state.Id) ? "TooltipCard" : $"TooltipCard_{state.Id}";

            // Sin arte el bloque del ícono se va entero, no sólo el Image: dejarlo prendido
            // reservaría su ancho en la fila y el título arrancaría corrido contra un hueco.
            // El estilo no entra en esta cuenta: Terrain dice de qué habla la tarjeta, no su forma.
            bool hasIcon = state.Icon != null;

            // Siempre a la izquierda, con o sin ícono: en una columna de tarjetas mezcladas los
            // títulos centrados bailaban de x según cuál tenía arte.
            if (_titleLabel != null)
                _titleLabel.text = state.DisplayName ?? string.Empty;

            if (!hasIcon)
            {
                if (_iconRoot != null) _iconRoot.SetActive(false);
                if (_badge != null) _badge.SetActive(false);
            }
            else
            {
                if (_iconRoot != null) _iconRoot.SetActive(true);

                if (_icon != null)
                {
                    _icon.sprite = state.Icon;
                    _icon.enabled = true;
                }

                if (_badge != null)
                {
                    // El stack va siempre acá, nunca dentro de la regla: si algún día traba
                    // dos dados en vez de uno, cambia este número y la frase no se toca.
                    string badge = StatusTooltipText.ResolveCardBadge(state);
                    if (_badgeLabel != null) _badgeLabel.text = badge;
                    _badge.SetActive(badge.Length > 0);
                }
            }

            if (_eyebrowLabel != null)
            {
                _eyebrowLabel.text = state.Eyebrow ?? string.Empty;
                _eyebrowLabel.gameObject.SetActive(!string.IsNullOrEmpty(state.Eyebrow));
            }

            // Nunca dentro de la frase: si algún día el disparo pega 30 en vez de 24, cambia este
            // número y no hay que retraducir nada. Es el mismo trato que el badge del stack.
            if (_damageLabel != null)
            {
                _damageLabel.text = state.Damage?.ToString() ?? string.Empty;
                _damageLabel.gameObject.SetActive(state.Damage.HasValue);
            }

            bool hasRule = !string.IsNullOrEmpty(state.Description);
            if (_ruleLabel != null)
            {
                _ruleLabel.text = state.Description ?? string.Empty;
                _ruleLabel.gameObject.SetActive(hasRule);
            }
            if (_divider != null) _divider.SetActive(hasRule);
        }
    }
}
