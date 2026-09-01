using System.Collections.Generic;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Combat.Threat
{
    /// <summary>
    /// Contenido del tooltip de un hazard de sala (lluvia, fuego de mesa, fichas). Mismo reparto
    /// que casillas y enemigos: <see cref="BuildContent"/> es el header y
    /// <see cref="CollectCards"/> los números como dato. Vive en el anchor de hover que el
    /// servicio de hazards le cuelga a cada instancia.
    /// </summary>
    /// <remarks>
    /// Se rearma en cada hover en vez de guardar el string: el idioma puede cambiar en pleno
    /// combate, misma razón que da <c>EnemyTooltipInfo</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Combat/Hazard Tooltip Info")]
    public sealed class HazardTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private readonly List<StatusIconState> _cards = new();

        private HazardDefinitionSO _definition;

        /// <summary>Llamado por quien arma el anchor al activar la instancia.</summary>
        public void Bind(HazardDefinitionSO definition) => _definition = definition;

        /// <summary>El header del panel: nombre, la fila "Peligro de sala" y su comportamiento.</summary>
        public TooltipContent BuildContent()
        {
            var def = _definition;
            if (def == null) return default;

            string id = string.IsNullOrEmpty(def.NameKey) ? def.SourceId : def.NameKey;

            return new TooltipContent(
                text: string.IsNullOrEmpty(def.DescriptionKey)
                    ? string.Empty
                    : LocalizedContent.Description(def.DescriptionKey, string.Empty),
                name: LocalizedContent.Name(id, def.DisplayName ?? def.name),
                type: LocalizedContent.Ui("hazard.panel.type", "Peligro de sala"),
                flavor: ComposeCadence(def));
        }

        /// <summary>El golpe como dato. Un hazard sin daño (las fichas) no saca tarjeta.</summary>
        public IReadOnlyList<StatusIconState> CollectCards()
        {
            _cards.Clear();
            var def = _definition;
            if (def != null && def.Damage > 0)
            {
                _cards.Add(new StatusIconState(
                    "hazard." + def.SourceId, LocalizedContent.Ui("hazard.panel.hit", "Golpe"),
                    description: null, icon: null, active: true,
                    style: StatusCardStyle.Terrain, damage: def.Damage,
                    eyebrow: LocalizedContent.Ui("tile.panel.effect", "Efecto")));
            }
            return _cards;
        }

        /// <summary>Fallback de texto plano para quien no cablea los providers.</summary>
        public string BuildTooltip()
        {
            var content = BuildContent();
            if (string.IsNullOrEmpty(content.Name)) return string.Empty;
            return string.IsNullOrEmpty(content.Text)
                ? $"<b>{content.Name}</b>"
                : $"<b>{content.Name}</b>\n{content.Text}";
        }

        // La cadencia va al pie: es del reloj de la sala, no un precio — mismo criterio que las
        // rondas restantes de una casilla temporal.
        private static string ComposeCadence(HazardDefinitionSO def)
        {
            if (def.Trigger != HazardTriggerMode.CycleTelegraph || def.CycleRounds <= 1) return null;
            return string.Format(
                LocalizedContent.Ui("hazard.panel.cycle", "Golpea cada {0} rondas"), def.CycleRounds);
        }
    }
}
