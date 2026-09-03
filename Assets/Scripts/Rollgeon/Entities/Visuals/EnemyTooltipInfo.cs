using System;
using System.Text;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// El panel (<see cref="BuildContent"/>) y el párrafo (<see cref="BuildTooltip"/>) del
    /// enemigo; lo pega <see cref="EntityVisualService"/> al spawnear. Guarda el SO y no el
    /// texto: se arma en cada hover para que un cambio de locale no lo deje viejo.
    /// </summary>
    [AddComponentMenu("Rollgeon/Entities/Enemy Tooltip Info")]
    public sealed class EnemyTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private EnemyDataSO _data;
        private Guid _guid;

        /// <summary>Llamado por <see cref="EntityVisualService"/> al instanciar el pawn.</summary>
        public void Bind(EnemyDataSO data, Guid guid)
        {
            _data = data;
            _guid = guid;
        }

        /// <summary>
        /// Nombre, familia, vida y la frase táctica (key <c>.brief</c>). Sin lore: cualquier
        /// resumen repite las tarjetas; la descripción larga vive en <see cref="BuildTooltip"/>.
        /// </summary>
        public TooltipContent BuildContent()
        {
            var data = _data;
            if (data == null) return default;

            string id = data.EntityId;
            string name = string.IsNullOrEmpty(id)
                ? data.DisplayName
                : LocalizedContent.Name(id, data.DisplayName);

            string brief = string.IsNullOrEmpty(id)
                ? string.Empty
                : LocalizedContent.FromTable(
                    LocalizedContent.ContentTable, id + ".brief", string.Empty);

            // Sin frase .brief autorada, el párrafo cae a la descripción de la ficha
            // (tabla .desc → SO). Antes el fallback era vacío a propósito; se cambió el
            // 03/09: lo que se autora en la tool de enemigos tiene que verse en el panel,
            // no solo en el codex.
            if (string.IsNullOrWhiteSpace(brief))
                brief = string.IsNullOrEmpty(id)
                    ? data.Description ?? string.Empty
                    : LocalizedContent.Description(id, data.Description);

            var (health, maxHealth) = ReadVitals();

            // Como párrafo y no como pie: el pie queda abajo de las tarjetas.
            return new TooltipContent(
                text: brief,
                name: name,
                health: health,
                maxHealth: maxHealth,
                type: ResolveTypeText(data));
        }

        /// <summary>
        /// Renglón de tipo/familia: tabla (<c>&lt;id&gt;.type</c>) → texto autorado en el
        /// SO (<see cref="EnemyDataSO.TooltipType"/>) → derivado del Archetype. El texto
        /// autorado va tal cual — quien lo escribe decide también el prefijo de Jefe.
        /// </summary>
        internal static string ResolveTypeText(EnemyDataSO data)
        {
            string id = data.EntityId;
            if (!string.IsNullOrEmpty(id))
            {
                string localized = LocalizedContent.FromTable(
                    LocalizedContent.ContentTable, id + ".type", string.Empty);
                if (!string.IsNullOrEmpty(localized)) return localized;
            }

            if (!string.IsNullOrWhiteSpace(data.TooltipType)) return data.TooltipType;

            return EnemyArchetypeText.Describe(data.Archetype, data.IsBoss);
        }

        // El max no vive en el atributo (el daño escribe sobre Health.Value): la referencia
        // tier-aware es la que todos los spawns dejan en el registry de AI. Se relee por hover.
        private (int? health, int? maxHealth) ReadVitals()
        {
            if (_guid == Guid.Empty) return (null, null);
            if (!ServiceLocator.TryGetService<IEnemyAIRegistry>(out var registry) || registry == null)
                return (null, null);
            if (!registry.TryGet(_guid, out _, out int max) || max <= 0) return (null, null);
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return (null, null);

            int current = Mathf.Clamp(attrs.GetAttributeValue<Health, int>(_guid), 0, max);
            return (current, max);
        }

        public string BuildTooltip()
        {
            var data = _data;
            if (data == null) return string.Empty;

            // Sin EntityId, el fallback autorado en el SO es todo lo que hay.
            string id = data.EntityId;
            string name = string.IsNullOrEmpty(id)
                ? data.DisplayName
                : LocalizedContent.Name(id, data.DisplayName);
            string description = string.IsNullOrEmpty(id)
                ? data.Description
                : LocalizedContent.Description(id, data.Description);

            // Vacío = el trigger no abre nada: mejor sin tooltip que el nombre del asset.
            bool hasName = !string.IsNullOrWhiteSpace(name);
            bool hasDescription = !string.IsNullOrWhiteSpace(description);
            if (!hasName && !hasDescription) return string.Empty;

            var sb = new StringBuilder();
            if (hasName) sb.Append("<b>").Append(name).Append("</b>");
            if (hasDescription)
            {
                if (hasName) sb.AppendLine();
                sb.Append(description);
            }
            return sb.ToString();
        }
    }
}
