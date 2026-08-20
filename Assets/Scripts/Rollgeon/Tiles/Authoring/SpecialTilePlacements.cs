using System;
using System.Collections.Generic;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Tiles.Authoring
{
    /// <summary>
    /// Casilla permanente colocada exacta por el diseñador (GDD: "autoría local decide
    /// dónde y cuáles"). Sin randomización de ningún tipo.
    /// </summary>
    [Serializable]
    public sealed class SpecialTilePlacement
    {
        public SpecialTileDefinitionSO Definition;
        public GridCoord Coord;
    }

    /// <summary>
    /// Slot con variación controlada: la POSICIÓN es fija; la aleatoriedad solo decide qué
    /// opción autorizada lo ocupa ("acá puede ir Pinchos o Fuego Temporal").
    /// </summary>
    [Serializable]
    public sealed class SpecialTileSlot
    {
        [Tooltip("Único por sala. Es la clave del roll determinista y del estado persistido " +
                 "en el save — renombrarlo re-rolea el slot en saves viejos.")]
        public string SlotId;

        public GridCoord Coord;

        [Tooltip("Grupo reutilizable de opciones (HazardGroup_A). Si está seteado, gana " +
                 "sobre InlineOptions.")]
        public SpecialTileOptionGroupSO Group;

        [Tooltip("Opciones locales del slot, si no usa un Group.")]
        public List<SpecialTileDefinitionSO> InlineOptions = new List<SpecialTileDefinitionSO>();

        [Tooltip("Grupo opcional: agrega 'nada' como resultado posible del roll.")]
        public bool CanResolveEmpty;

        public IReadOnlyList<SpecialTileDefinitionSO> EffectiveOptions
            => Group != null ? (IReadOnlyList<SpecialTileDefinitionSO>)Group.Options : InlineOptions;
    }

    /// <summary>
    /// Par de portales conectados como UN registro con dos coords — el portal huérfano es
    /// imposible por construcción: borrar el registro borra ambos extremos, y no existe
    /// "medio par" serializable.
    /// </summary>
    [Serializable]
    public sealed class PortalPairPlacement
    {
        public SpecialTileDefinitionSO PortalDefinition;
        public GridCoord CoordA;
        public GridCoord CoordB;
    }
}
