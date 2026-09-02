using System.Collections.Generic;
using Rollgeon.Dice;
using Rollgeon.Upgrades.Dice;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Especificación de alta de un encantamiento. Campos públicos planos, sin tipos de
    /// UI, a propósito: tiene que sobrevivir un round trip de JSON — la arman tanto el
    /// formulario de la ventana como la skill MCP (espejo de <c>ItemCreationSpec</c>).
    /// </summary>
    /// <remarks>
    /// Deltas contra items: no hay rareza ni precio (el costo del altar es global en
    /// <c>EnchantmentConfigSO</c>; el dial de balance es <see cref="PoolWeight"/> +
    /// <see cref="MinFloorDepth"/>), no hay familia de variantes (la agrupación es la
    /// <see cref="Category"/> del GDD), y la categoría es obligatoria — la auditoría
    /// rechaza <c>None</c>.
    /// </remarks>
    public struct EnchantmentCreationSpec
    {
        /// <summary>Nombre en español. Deriva el id (<c>ench.&lt;snake_case&gt;</c>) — se congela al crear.</summary>
        public string DisplayName;

        /// <summary>Descripción en español (tooltip del altar/bolsa).</summary>
        public string Description;

        /// <summary>Nombre en inglés. Vacío = se siembra el español y la suite de loc queda roja hasta traducir.</summary>
        public string DisplayNameEn;

        /// <summary>Descripción en inglés.</summary>
        public string DescriptionEn;

        /// <summary>Opcional. Null hasta el pipeline de arte.</summary>
        public Sprite Icon;

        /// <summary>Categoría GDD (Caos/Recursos/Ataque/Control/Movimiento). Obligatoria ≠ None.</summary>
        public EnchantmentCategory Category;

        /// <summary>Tipos de dado a los que aplica. Null o vacío = todos.</summary>
        public IReadOnlyList<DiceType> AllowedDiceTypes;

        /// <summary>Peso de aparición en el pool del altar. Null = 1. 0 = registrado pero deshabilitado.</summary>
        public float? PoolWeight;

        /// <summary>Piso mínimo desde el que puede ofrecerse. Null = 0.</summary>
        public int? MinFloorDepth;

        /// <summary>Null = <c>EnchantmentAuthoring.DefaultFolder</c>.</summary>
        public string TargetFolder;

        /// <summary>
        /// El CUANDO. Id de <see cref="EnchantmentTriggerCatalog"/>; vacío = nace sin
        /// triggers (válido para encantamientos de solo-FaceFilter o solo-capabilities).
        /// Un id fuera del catálogo falla la creación, no crea un encantamiento mudo.
        /// </summary>
        public string TriggerId;

        /// <summary>Solo con la opción que pide combos (<c>UsesComboIds</c>).</summary>
        public IReadOnlyList<string> TriggerComboIds;

        /// <summary>
        /// Solo dispara si el dado portador participó del combo. Solo aplica a los
        /// disparadores de combo; obligatorio si los efectos van a usar <c>PcCarrierFace</c>
        /// (la auditoría lo exige).
        /// </summary>
        public bool RequireCarrierParticipates;
    }
}
