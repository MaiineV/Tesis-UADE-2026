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
    /// Lo que un enemigo sabe decir de sí mismo, en las dos formas que le piden: el panel
    /// (<see cref="BuildContent"/>) y el párrafo (<see cref="BuildTooltip"/>).
    /// Lo pega <see cref="EntityVisualService"/> en el pawn al spawnearlo, junto a un
    /// <see cref="WorldTooltipTrigger"/> en modo Hover; el <c>TooltipResolver</c> lo encuentra solo.
    /// </summary>
    /// <remarks>
    /// <b>Guarda el SO, no el texto.</b> El texto se arma en cada hover para que el idioma
    /// vigente mande: cacheado al spawnear, un cambio de locale a mitad de combate deja al
    /// enemigo describiéndose en el idioma anterior.
    /// </remarks>
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
        /// El contenido del panel: nombre, familia, la vida leída en este hover y una frase
        /// táctica de una línea (la key <c>.brief</c>). Sin lore: cualquier frase que resuma
        /// al bicho repite sus tarjetas. La descripción larga vive en <see cref="BuildTooltip"/>
        /// (bombas y objetos del jefe, donde el tooltip es un párrafo y no un panel).
        /// </summary>
        public TooltipContent BuildContent()
        {
            var data = _data;
            if (data == null) return default;

            string id = data.EntityId;
            string name = string.IsNullOrEmpty(id)
                ? data.DisplayName
                : LocalizedContent.Name(id, data.DisplayName);

            // Sin entry no se dibuja nada: el fallback vacío ES la decisión de que un enemigo
            // sin frase autorada no muestre una.
            string brief = string.IsNullOrEmpty(id)
                ? string.Empty
                : LocalizedContent.FromTable(
                    LocalizedContent.ContentTable, id + ".brief", string.Empty);

            var (health, maxHealth) = ReadVitals();

            // Como párrafo y no como pie: el párrafo vive pegado a la identidad; el pie queda
            // abajo de las tarjetas.
            return new TooltipContent(
                text: brief,
                name: name,
                health: health,
                maxHealth: maxHealth,
                type: EnemyArchetypeText.Describe(data.Archetype, data.IsBoss));
        }

        // El max no vive en el atributo — el daño escribe sobre Health.Value — así que la
        // referencia tier-aware es la que todos los caminos de spawn dejan en el registry de AI.
        // Se relee en cada hover, como el resto del contenido: el número sale fresco aunque el
        // panel venga fijado del turno pasado.
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

            // El EntityId es la key de localización de la familia Name/Description; sin él, el
            // fallback autorado en el SO es todo lo que hay.
            string id = data.EntityId;
            string name = string.IsNullOrEmpty(id)
                ? data.DisplayName
                : LocalizedContent.Name(id, data.DisplayName);
            string description = string.IsNullOrEmpty(id)
                ? data.Description
                : LocalizedContent.Description(id, data.Description);

            // Sin nombre ni descripción se devuelve vacío y el trigger no abre nada: un panel con
            // el nombre del asset adentro es peor que la ausencia de tooltip.
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
