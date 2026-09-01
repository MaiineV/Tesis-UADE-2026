using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Encantamiento del item activo. GDD "Ítems Activos" §25: <b>maximo 1 por item</b>,
    /// se pisa al aplicar otro, y se pierde con el item si el jugador lo reemplaza.
    /// </summary>
    /// <remarks>
    /// Pool propio, separado de la lista de 33 encantamientos de los dados de combate
    /// (§23). Se aplica en la misma mesa de la Sala de Encantamientos, pero no comparte
    /// opciones.
    /// <para>
    /// <b>No vive en el <see cref="ItemSO"/>.</b> El ItemSO es un asset compartido del
    /// catalogo; el encantamiento es estado de run y viaja con el slot equipado, no con
    /// la definicion del item.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Items/Active Item Enchantment",
        fileName = "ActiveItemEnchantment")]
    public sealed class ActiveItemEnchantmentSO : SerializedScriptableObject
    {
        [Title("Identidad")]
        public string EnchantmentId;
        public string DisplayName;
        [TextArea] public string Description;

        [Title("Efecto sobre la tirada")]
        [InfoBox("Ajusta el resultado ANTES de determinar la banda. No puede sacar el " +
                 "resultado del rango del dado.")]
        [OdinSerialize, SerializeReference]
        public ActiveItemRollModifier Modifier;

        [Title("Limite de uso")]
        [InfoBox("Usos por combate. 0 = sin limite. El GDD pide que los usos limitados " +
                 "reseteen entre combates, no que se agoten para toda la run.")]
        [MinValue(0)]
        public int UsesPerCombat = 0;

        /// <summary><c>true</c> si el encantamiento tiene un tope de usos por combate.</summary>
        public bool IsLimited => UsesPerCombat > 0;

        /// <summary>Texto corto para el tooltip de la ficha.</summary>
        public string DescribeEffect()
        {
            string body = Modifier != null ? Modifier.Describe() : "sin efecto";
            return IsLimited ? $"{body} ({UsesPerCombat}×/combate)" : body;
        }
    }
}
