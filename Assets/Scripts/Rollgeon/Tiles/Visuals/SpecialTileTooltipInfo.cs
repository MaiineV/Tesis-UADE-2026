using System.Text;
using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Tiles.Visuals
{
    /// <summary>
    /// Contenido del tooltip de una casilla especial (GDD §16: tooltip + daño esperado +
    /// duración). Va en el prefab visual junto a un <c>WorldTooltipTrigger</c> (que
    /// necesita un Collider); el <c>TooltipResolver</c> lo encuentra solo.
    /// </summary>
    [AddComponentMenu("Rollgeon/Tiles/Special Tile Tooltip Info")]
    public sealed class SpecialTileTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private SpecialTileDefinitionSO _definition;
        private int _remainingRounds;

        /// <summary>Llamado por el servicio al instanciar el visual.</summary>
        public void Bind(SpecialTileDefinitionSO definition, int remainingRounds)
        {
            _definition = definition;
            _remainingRounds = remainingRounds;
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
            if (def.EnterDamage > 0)
                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("tile.tooltip.enterdamage", "Daño al entrar: {0}"), def.EnterDamage));
            if (def.TurnStartDamage > 0)
                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("tile.tooltip.turndamage", "Daño por turno encima: {0}"), def.TurnStartDamage));
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
