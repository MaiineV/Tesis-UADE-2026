using System;
using System.Collections.Generic;
using System.Text;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Combat.Rooms.Visuals
{
    /// <summary>
    /// Contenido del tooltip de un objeto que un jefe pone en la sala — la bomba del Croupier y su
    /// mecha.
    /// </summary>
    /// <remarks>
    /// Se rearma en cada hover en vez de guardar el string: el idioma puede cambiar en pleno
    /// combate, misma razón que da <c>EnemyTooltipInfo</c>.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Rooms/Room Object Tooltip Info")]
    public sealed class RoomObjectTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private readonly List<AIIntent> _standing = new();
        private readonly List<AIIntent> _next = new();

        private RoomObjectDefinitionSO _definition;
        private Guid _ownerGuid;
        private Guid _selfGuid;

        /// <summary>
        /// <paramref name="ownerGuid"/> es el jefe que lo puso: es a su árbol al que hay que
        /// preguntarle cuánto le queda a la mecha.
        /// </summary>
        public void Bind(RoomObjectDefinitionSO definition, Guid ownerGuid, Guid selfGuid)
        {
            _definition = definition;
            _ownerGuid = ownerGuid;
            _selfGuid = selfGuid;
        }

        public string BuildTooltip()
        {
            var def = _definition;
            if (def == null) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("<b>").Append(LocalizedContent.Name(def.Id, def.EffectiveDisplayName)).Append("</b>");

            string description = LocalizedContent.Description(def.Id, string.Empty);
            if (!string.IsNullOrEmpty(description)) sb.AppendLine().Append(description);

            AppendHealth(sb);
            AppendBlast(sb);
            return sb.ToString();
        }

        private void AppendHealth(StringBuilder sb)
        {
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attributes) || attributes == null)
                return;

            var health = attributes.GetAttribute<Health>(_selfGuid);
            if (health == null) return;

            sb.AppendLine().Append(string.Format(
                LocalizedContent.Ui("prop.tooltip.health", "Vida: {0}"), health.Value));
        }

        private void AppendBlast(StringBuilder sb)
        {
            if (!ServiceLocator.TryGetService<IEnemyIntentService>(out var intents) || intents == null)
                return;
            if (!intents.TryRead(_ownerGuid, _standing, _next)) return;

            foreach (var intent in _standing)
            {
                if (intent.SubjectGuid != _selfGuid) continue;

                sb.AppendLine().Append(string.Format(
                    LocalizedContent.Ui("prop.tooltip.fuse", "Estalla en {0} turnos"),
                    Mathf.Max(0, intent.TurnsAway)));

                // El estallido en sí no cobra nada: todo lo que hace una bomba es el fuego que
                // deja. Anunciar un golpe sería prometer un número que no existe.
                var fire = intent.Leaves;
                if (fire == null) return;

                if (fire.EnterDamage > 0)
                    sb.AppendLine().Append(string.Format(
                        LocalizedContent.Ui("tile.tooltip.enterdamage", "Daño al entrar: {0}"),
                        fire.EnterDamage));
                if (fire.TurnStartDamage > 0)
                    sb.AppendLine().Append(string.Format(
                        LocalizedContent.Ui("tile.tooltip.turndamage", "Daño por turno encima: {0}"),
                        fire.TurnStartDamage));
                if (intent.LeavesRounds > 0)
                    sb.AppendLine().Append(string.Format(
                        LocalizedContent.Ui("tile.tooltip.duration", "Dura {0} rondas"),
                        intent.LeavesRounds));
                return;
            }
        }
    }
}
