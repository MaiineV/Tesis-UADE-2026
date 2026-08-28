using System.Text;
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
    /// <para>
    /// <b>Guarda el SO, no el texto.</b> El texto se arma en cada hover para que el idioma vigente
    /// mande: si se cachea al spawnear, cambiar de locale a mitad de un combate deja al enemigo
    /// describiéndose en el idioma anterior hasta el próximo combate.
    /// </para>
    /// <para>
    /// La descripción tiene que decir lo que el bicho <i>hace ahora</i>. Un rediseño que cambia el
    /// kit y deja la descripción vieja es peor que no tener tooltip: promete una pelea que no
    /// existe. Y donde sí se lee —el párrafo— no hay tarjetas al lado que la desmientan.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/Entities/Enemy Tooltip Info")]
    public sealed class EnemyTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private EnemyDataSO _data;

        /// <summary>Llamado por <see cref="EntityVisualService"/> al instanciar el pawn.</summary>
        public void Bind(EnemyDataSO data) => _data = data;

        /// <summary>
        /// El contenido del panel: nombre y familia. <b>Sin descripción y sin vitales.</b>
        /// </summary>
        /// <remarks>
        /// El panel no lleva lore, y no es una cuestión de espacio: cualquier frase que resuma al
        /// bicho repite alguna de sus tarjetas, porque las tarjetas <i>son</i> lo que hace. El
        /// Croupier se describe con tres verbos y uno de los tres es siempre el ataque que se está
        /// mostrando — teníamos "…y dispara de lejos" a un renglón de "Te dispara de lejos".
        /// <para>
        /// La descripción sigue viva y se lee por <see cref="BuildTooltip"/>, que es lo que usan
        /// las bombas y los objetos que un jefe pone en el paño: ahí el tooltip es un párrafo y no
        /// un panel, así que no hay tarjeta que pueda contradecirla.
        /// </para>
        /// <para>
        /// <b>Ni vitales.</b> La barra de vida ya flota sobre la cabeza del bicho y es la que el
        /// jugador mira mientras le pega; repetirla adentro del panel gasta una fila en un número
        /// que está a dos centímetros, y encima desactualizado hasta el próximo hover.
        /// </para>
        /// </remarks>
        public TooltipContent BuildContent()
        {
            var data = _data;
            if (data == null) return default;

            string id = data.EntityId;
            string name = string.IsNullOrEmpty(id)
                ? data.DisplayName
                : LocalizedContent.Name(id, data.DisplayName);

            return new TooltipContent(
                name: name,
                type: EnemyArchetypeText.Describe(data.Archetype, data.IsBoss));
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
