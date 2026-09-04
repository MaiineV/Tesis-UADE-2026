using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
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
        /// <summary>Rótulos de la tarjeta de reloj.</summary>
        public const string ClockTicksKey = "hazard.panel.clock_ticks";
        public const string ClockDueKey = "hazard.panel.clock_due";

        private readonly List<StatusIconState> _cards = new();
        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();

        private HazardDefinitionSO _definition;
        private Guid _instanceId;
        private Guid _ownerGuid;

        /// <summary>Sin dueño a quien preguntarle (hazard de sala) la ficha va sin reloj.</summary>
        public void Bind(HazardDefinitionSO definition, Guid instanceId = default,
                         Guid ownerGuid = default)
        {
            _definition = definition;
            _instanceId = instanceId;
            _ownerGuid = ownerGuid;
        }

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

        /// <summary>Arriba el reloj de esta instancia si su dueño lo publica; abajo el golpe. Un hazard sin daño no saca la segunda.</summary>
        public IReadOnlyList<StatusIconState> CollectCards()
        {
            _cards.Clear();
            var def = _definition;

            if (TryReadOwnClock(out int turnsLeft)) AppendClockCard(turnsLeft);

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

        /// <summary>El reloj de ESTA instancia, buscado por subject igual que la mecha de cada bomba.</summary>
        private bool TryReadOwnClock(out int turnsLeft)
        {
            turnsLeft = 0;
            if (_ownerGuid == Guid.Empty || _instanceId == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return false;
            if (!intents.TryRead(_ownerGuid, _standing, _next)) return false;

            foreach (var intent in _standing)
            {
                if (intent.SubjectGuid != _instanceId) continue;
                turnsLeft = intent.TurnsAway;
                return true;
            }
            return false;
        }

        // 0 turnos no lleva badge: el título ya dice que se la lleva ahora.
        private void AppendClockCard(int turnsLeft)
        {
            bool due = turnsLeft <= 0;

            _cards.Add(new StatusIconState(
                due ? ClockDueKey : ClockTicksKey,
                due
                    ? LocalizedContent.Ui(ClockDueKey, "Se la lleva la caja")
                    : LocalizedContent.Ui(ClockTicksKey, "Se vence"),
                description: null,
                icon: null,
                active: true,
                remainingTurns: due ? (int?)null : turnsLeft,
                style: StatusCardStyle.Terrain,
                eyebrow: EnemyStatusIconsView.NextTurnEyebrow()));
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
