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

        [Tooltip("Lo que pega, a la derecha del título.")]
        [SerializeField] private TextMeshProUGUI _damageLabel;

        [Tooltip("Renglón chico arriba del título — 'Próximo turno'.")]
        [SerializeField] private TextMeshProUGUI _eyebrowLabel;

        [Tooltip("Línea debajo del label del bloque.")]
        [SerializeField] private GameObject _divider;

        [Tooltip("Fila ícono + eyebrow. Null en prefabs sin la fila.")]
        [SerializeField] private GameObject _labelRow;

        [Tooltip("Fila título + daño. Se apaga en tarjetas de sólo label y regla.")]
        [SerializeField] private GameObject _headerRow;

        /// <summary>Id del estado que esta tarjeta está mostrando — la columna lo usa para reusarla.</summary>
        public string CardId { get; private set; }

        /// <summary>
        /// El estado completo que la tarjeta muestra ahora. <see cref="TooltipStatusSlotHover"/>
        /// arma su burbuja de acá — la tarjeta ya lo tiene y pedirlo de vuelta al provider
        /// desincronizaría burbuja y placa.
        /// </summary>
        public StatusIconState CurrentState { get; private set; }

        public void Show(in StatusIconState state)
        {
            CardId = state.Id;
            CurrentState = state;
            name = string.IsNullOrEmpty(state.Id) ? "TooltipCard" : $"TooltipCard_{state.Id}";

            // Sin arte el bloque del ícono se va entero: prendido reservaría su ancho y el
            // título arrancaría corrido contra un hueco.
            bool hasIcon = state.Icon != null;

            // Siempre a la izquierda: centrados, bailaban de x según cuál tenía arte.
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
                    // El stack nunca va dentro de la regla: cambia el número, no la frase.
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

            // Sin ícono ni eyebrow la fila entera se va, no queda un renglón de aire.
            if (_labelRow != null) _labelRow.SetActive(hasIcon || hasEyebrow);

            // Nunca dentro de la frase: cambiar el número no obliga a retraducir.
            if (_damageLabel != null)
            {
                _damageLabel.text = state.Damage.HasValue
                    ? Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(state.Damage.Value)
                    : string.Empty;
                _damageLabel.gameObject.SetActive(state.Damage.HasValue);

                // El submesh del sprite inline nace en runtime y con el rect crudo el
                // ícono queda despegado del número — el offset calibrado lo sostiene
                // este componente (los slots se instancian por código, no hay prefab
                // que lo traiga puesto).
                if (state.Damage.HasValue
                    && !_damageLabel.TryGetComponent<TMPSubMeshRectOffset>(out _))
                    _damageLabel.gameObject.AddComponent<TMPSubMeshRectOffset>();
            }

            if (_headerRow != null) _headerRow.SetActive(hasTitle || state.Damage.HasValue);

            bool hasRule = !string.IsNullOrEmpty(state.Description);
            if (_ruleLabel != null)
            {
                _ruleLabel.text = state.Description ?? string.Empty;
                _ruleLabel.gameObject.SetActive(hasRule);
            }

            // Se prende con label arriba y algo abajo; sin label la línea sobra.
            if (_divider != null)
                _divider.SetActive(hasEyebrow && (hasTitle || hasRule || state.Damage.HasValue));
        }
    }
}
