using Rollgeon.UI.HUD.Status;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tooltips
{
    /// <summary>
    /// Una tarjeta de la columna del tooltip: una sola cosa en juego, nunca un párrafo.
    /// <see cref="StatusIconState.Style"/> decide la forma — <see cref="StatusCardStyle.Unit"/>
    /// habla de la unidad (ícono + título a la izquierda) y <see cref="StatusCardStyle.Terrain"/>
    /// habla del suelo (sin ícono, título centrado).
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

        /// <summary>Id del estado que esta tarjeta está mostrando — la columna lo usa para reusarla.</summary>
        public string CardId { get; private set; }

        public void Show(in StatusIconState state)
        {
            CardId = state.Id;
            name = string.IsNullOrEmpty(state.Id) ? "TooltipCard" : $"TooltipCard_{state.Id}";

            bool isTerrain = state.Style == StatusCardStyle.Terrain;

            if (_titleLabel != null)
            {
                _titleLabel.text = state.DisplayName ?? string.Empty;
                _titleLabel.alignment = isTerrain
                    ? TextAlignmentOptions.Center
                    : TextAlignmentOptions.Left;
            }

            if (isTerrain)
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
                    // Un estado sin arte todavía no debe dibujar el cuadrado blanco del Image.
                    _icon.enabled = state.Icon != null;
                }

                if (_badge != null)
                {
                    // El stack va siempre acá, nunca dentro de la regla: si algún día traba
                    // dos dados en vez de uno, cambia este número y la frase no se toca.
                    string badge = StatusTooltipText.ResolveBadge(state);
                    if (_badgeLabel != null) _badgeLabel.text = badge;
                    _badge.SetActive(badge.Length > 0);
                }
            }

            if (_ruleLabel != null)
            {
                _ruleLabel.text = state.Description ?? string.Empty;
                _ruleLabel.gameObject.SetActive(!string.IsNullOrEmpty(state.Description));
            }
        }
    }
}
