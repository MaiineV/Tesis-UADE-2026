using Rollgeon.Items;
using Rollgeon.Localization;
using Rollgeon.UI.ChestReveal;
using Rollgeon.UI.Tooltips;
using UnityEngine;

namespace Rollgeon.Chests
{
    /// <summary>
    /// Contenido del tooltip de un cofre: nombre, rareza y una línea. Lo cuelga
    /// <see cref="ChestService"/> al spawnear.
    /// </summary>
    /// <remarks>
    /// <b>A prueba de mímico por construcción:</b> el cofre real y el mímico camuflado pasan por
    /// el MISMO spawn con el MISMO componente, y este contenido sale sólo del tier y de keys
    /// fijas <c>chest.*</c> — nunca de <c>IsMimic</c> ni del <c>EnemyDataSO</c> del mímico
    /// (cuyas keys <c>ChestMimic01.*</c> son el disfraz al descubierto en texto). Cualquier dato
    /// nuevo que se agregue acá tiene que existir idéntico en los dos, o el hover se convierte
    /// en detector de mímicos gratis. Por lo mismo, nada de vitales: un mímico golpeado por otro
    /// enemigo queda clavado en 1 HP mientras sigue disfrazado.
    /// </remarks>
    [AddComponentMenu("Rollgeon/Chests/Chest Tooltip Info")]
    public sealed class ChestTooltipInfo : MonoBehaviour, IHasTooltipInfo
    {
        private ItemRarity _tier;

        /// <summary>Llamado por <see cref="ChestService"/> al spawnear el cofre.</summary>
        public void Bind(ItemRarity tier) => _tier = tier;

        /// <summary>El header del panel. Se rearma en cada hover: el idioma puede cambiar en vivo.</summary>
        public TooltipContent BuildContent()
            => new TooltipContent(
                text: LocalizedContent.Description("chest", "Rompelo y fijate qué guarda."),
                name: LocalizedContent.Name("chest", "Cofre"),
                type: LocalizedContent.Ui(ChestRevealTextKeys.RarityKey(_tier), _tier.ToString()));

        /// <summary>Fallback de texto plano para quien no cablea los providers.</summary>
        public string BuildTooltip()
        {
            var content = BuildContent();
            return $"<b>{content.Name}</b>\n{content.Text}";
        }
    }
}
