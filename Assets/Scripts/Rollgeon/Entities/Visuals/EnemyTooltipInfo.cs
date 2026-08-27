using System;
using System.Text;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Localization;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Entities.Visuals
{
    /// <summary>
    /// Contenido del tooltip de un enemigo: nombre + descripción de su <see cref="EnemyDataSO"/>.
    /// Lo pega <see cref="EntityVisualService"/> en el pawn al spawnearlo, junto a un
    /// <see cref="WorldTooltipTrigger"/> en modo Hover; el <c>TooltipResolver</c> lo encuentra solo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Guarda el SO, no el texto.</b> El texto se arma en cada hover para que el idioma vigente
    /// mande: si se cachea al spawnear, cambiar de locale a mitad de un combate deja al enemigo
    /// describiéndose en el idioma anterior hasta el próximo combate.
    /// </para>
    /// <para>
    /// La descripción es la única explicación de la pelea que el jugador puede leer sin morir
    /// primero, así que tiene que decir lo que el jefe <i>hace ahora</i>. Un rediseño que cambia el
    /// kit y deja la descripción vieja es peor que no tener tooltip: promete una pelea que no existe.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/Entities/Enemy Tooltip Info")]
    public sealed class EnemyTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private EnemyDataSO _data;
        private Guid _entityGuid;

        /// <summary>Llamado por <see cref="EntityVisualService"/> al instanciar el pawn.</summary>
        public void Bind(EnemyDataSO data) => _data = data;

        /// <summary>
        /// Con el guid, además del texto puede leer los vitales. Sobrecarga y no un parámetro
        /// más: los tests que sólo miran el texto no tienen guid que dar.
        /// </summary>
        public void Bind(EnemyDataSO data, Guid entityGuid)
        {
            _data = data;
            _entityGuid = entityGuid;
        }

        /// <summary>
        /// El contenido completo: identidad y vitales arriba, color al pie. La descripción baja
        /// al pie porque no es información — el jugador que abre el panel a mitad de una pelea
        /// viene a ver cuánta vida le queda al bicho, no a leer su presentación.
        /// </summary>
        public TooltipContent BuildContent()
        {
            var data = _data;
            if (data == null) return default;

            string id = data.EntityId;
            string name = string.IsNullOrEmpty(id)
                ? data.DisplayName
                : LocalizedContent.Name(id, data.DisplayName);
            string flavor = string.IsNullOrEmpty(id)
                ? data.Description
                : LocalizedContent.Description(id, data.Description);

            ReadVitals(out int? health, out int? maxHealth, out int? shield);
            return new TooltipContent(name: name, flavor: flavor,
                                      health: health, maxHealth: maxHealth, shield: shield);
        }

        // Sin AttributesManager los tres quedan en null y la banda no dibuja la fila: fuera de
        // combate el enemigo no tiene vitales que mostrar, y un "0/0" seria peor que nada.
        private void ReadVitals(out int? health, out int? maxHealth, out int? shield)
        {
            health = null;
            maxHealth = null;
            shield = null;

            if (_entityGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return;

            int max = attrs.GetAttributeValue<MaxHealth, int>(_entityGuid);
            if (max <= 0) return;

            maxHealth = max;
            health = attrs.GetAttributeValue<Health, int>(_entityGuid);
            shield = attrs.GetAttributeValue<Shield, int>(_entityGuid);
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
