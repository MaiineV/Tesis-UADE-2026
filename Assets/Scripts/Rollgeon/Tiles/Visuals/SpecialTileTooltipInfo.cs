using System.Collections.Generic;
using System.Text;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Tiles.Visuals
{
    /// <summary>
    /// Contenido del tooltip de una casilla especial (GDD §16: tooltip + daño esperado +
    /// duración). Va en el prefab visual junto a un <c>WorldTooltipTrigger</c> (que
    /// necesita un Collider); el <c>TooltipResolver</c> lo encuentra solo.
    /// </summary>
    /// <remarks>
    /// Mismo reparto que el panel de un enemigo: <see cref="BuildContent"/> es el header
    /// (nombre + categoría + descripción) y <see cref="CollectCards"/> las tarjetas con los
    /// números. <see cref="BuildTooltip"/> queda como fallback de texto plano para quien no
    /// cablea los providers.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Tiles/Special Tile Tooltip Info")]
    public sealed class SpecialTileTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private readonly List<StatusIconState> _cards = new();

        private SpecialTileDefinitionSO _definition;
        private int _remainingRounds;

        /// <summary>Llamado por el servicio al instanciar el visual.</summary>
        public void Bind(SpecialTileDefinitionSO definition, int remainingRounds)
        {
            _definition = definition;
            _remainingRounds = remainingRounds;
        }

        /// <summary>El header del panel. Se rearma en cada hover: el idioma puede cambiar en vivo.</summary>
        public TooltipContent BuildContent()
        {
            var def = _definition;
            if (def == null) return default;

            string id = string.IsNullOrEmpty(def.NameKey) ? def.TileId : def.NameKey;

            // La vida restante va al pie y no a una tarjeta: es del sistema de rondas, no un
            // precio de la casilla.
            string flavor = _remainingRounds > 0
                ? string.Format(LocalizedContent.Ui("tile.tooltip.duration", "Dura {0} rondas"),
                                _remainingRounds)
                : null;

            return new TooltipContent(
                text: LocalizedContent.Description(id, string.Empty),
                name: LocalizedContent.Name(id, def.DisplayName ?? def.TileId),
                type: TileCategoryText.Describe(def.Category),
                flavor: flavor);
        }

        /// <summary>Las tarjetas de números (ver <see cref="SpecialTileCards"/>).</summary>
        public IReadOnlyList<StatusIconState> CollectCards()
        {
            _cards.Clear();
            SpecialTileCards.Append(_definition, _cards);
            return _cards;
        }

        public string BuildTooltip()
        {
            var def = _definition;
            if (def == null) return string.Empty;

            string id = string.IsNullOrEmpty(def.NameKey) ? def.TileId : def.NameKey;
            var sb = new StringBuilder();
            sb.Append("<b>").Append(LocalizedContent.Name(id, def.DisplayName ?? def.TileId)).Append("</b>");

            string description = LocalizedContent.Description(id, string.Empty);
            if (!string.IsNullOrEmpty(description))
                sb.AppendLine().Append(description);

            // Números duros del GDD §16: daño esperado / curación visible en el tooltip.
            // Los montos de daño viajan con el indicador pegado a la derecha (IconSpriteTags).
            if (def.EnterDamage > 0)
                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("tile.tooltip.enterdamage", "Daño al entrar: {0}"),
                    Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(def.EnterDamage)));
            if (def.TurnStartDamage > 0)
                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("tile.tooltip.turndamage", "Daño por turno encima: {0}"),
                    Rollgeon.UI.Utility.IconSpriteTags.DamageAmount(def.TurnStartDamage)));
            if (def.HealAmount > 0)
                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("tile.tooltip.heal", "Cura al terminar el turno: {0}"), def.HealAmount));
            if (_remainingRounds > 0)
                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("tile.tooltip.duration", "Dura {0} rondas"), _remainingRounds));

            return sb.ToString();
        }
    }
}
