using System.Collections.Generic;
using UnityEngine;

namespace Rollgeon.Tiles.Authoring
{
    /// <summary>
    /// Grupo reutilizable de opciones para slots de casillas especiales
    /// (<c>HazardGroup_A</c>, <c>BenefitGroup_B</c> del GDD): un conjunto cerrado de
    /// casillas intercambiables entre sí. Un slot que referencia el grupo hereda su lista
    /// — compartirla entre salas sin duplicar autoría.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Tiles/Special Tile Option Group", fileName = "SpecialTileGroup")]
    public sealed class SpecialTileOptionGroupSO : ScriptableObject
    {
        [Tooltip("Id legible del grupo (HazardGroup_A). Solo informativo/validación.")]
        public string GroupId;

        [Tooltip("Opciones intercambiables. La aleatoriedad SOLO elige entre estas — nunca " +
                 "una casilla o una posición fuera de lo autorizado.")]
        public List<SpecialTileDefinitionSO> Options = new List<SpecialTileDefinitionSO>();
    }
}
