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
    /// acá puede depender de él.
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

        [Tooltip("Línea debajo del label del bloque (NEXT TURN, PLAYER CURSE). Se apaga cuando " +
                 "no hay label o no hay nada debajo que separar.")]
        [SerializeField] private GameObject _divider;

        [Tooltip("La fila del label del bloque — ícono + eyebrow. Se apaga entera cuando el " +
                 "estado no trae ninguno de los dos. Null en prefabs sin la fila: cada pieza " +
                 "se apaga sola.")]
        [SerializeField] private GameObject _labelRow;

        [Tooltip("La fila del título y el daño. Se apaga entera en las tarjetas que son sólo " +
                 "label y regla — la maldición del jefe no lleva título.")]
        [SerializeField] private GameObject _headerRow;

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
            bool hasTitle = !string.IsNullOrEmpty(state.DisplayName);
            if (_titleLabel != null)
            {
                _titleLabel.text = state.DisplayName ?? string.Empty;
                _titleLabel.gameObject.SetActive(hasTitle);
            }

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

            bool hasEyebrow = !string.IsNullOrEmpty(state.Eyebrow);
            if (_eyebrowLabel != null)
            {
                _eyebrowLabel.text = state.Eyebrow ?? string.Empty;
                _eyebrowLabel.gameObject.SetActive(hasEyebrow);
            }

            // El ícono vive en la fila del label (el candado al lado de PLAYER CURSE): sin
            // ninguno de los dos la fila entera se va, no queda un renglón de aire.
            if (_labelRow != null) _labelRow.SetActive(hasIcon || hasEyebrow);

            // Nunca dentro de la frase: si algún día el disparo pega 30 en vez de 24, cambia este
            // número y no hay que retraducir nada. Es el mismo trato que el badge del stack.
            if (_damageLabel != null)
            {
                _damageLabel.text = state.Damage?.ToString() ?? string.Empty;
                _damageLabel.gameObject.SetActive(state.Damage.HasValue);
            }

            if (_headerRow != null) _headerRow.SetActive(hasTitle || state.Damage.HasValue);

            bool hasRule = !string.IsNullOrEmpty(state.Description);
            if (_ruleLabel != null)
            {
                _ruleLabel.text = state.Description ?? string.Empty;
                _ruleLabel.gameObject.SetActive(hasRule);
            }

            // El divisor subraya el label del bloque, no parte el contenido: se prende con label
            // arriba y algo abajo. Sin label la tarjeta es de un solo bloque y la línea sobra.
            if (_divider != null)
                _divider.SetActive(hasEyebrow && (hasTitle || hasRule || state.Damage.HasValue));
        }
    }
}
