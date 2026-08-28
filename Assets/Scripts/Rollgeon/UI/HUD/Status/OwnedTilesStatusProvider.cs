using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Localization;
using Rollgeon.Tiles;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica lo que una unidad dejó ardiendo en el paño: el fuego que sigue en el piso y es suyo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El nodo que prende sólo sabe describirse <b>mientras marca</b> la banda: su intent sale de
    /// un área amenazada, y apenas prende la marca se consume. Es decir que la tarjeta del fuego
    /// desaparecía exactamente cuando empezaba a haber fuego. Este provider lee el otro lado — las
    /// casillas ya puestas — así que el panel dice "Quemadura" desde el turno en que arde y hasta
    /// que se apaga.
    /// </para>
    /// <para>
    /// Va por <see cref="SpecialTileInfo.OwnerGuid"/> y no por proximidad: el fuego del Croupier se
    /// lee en el Croupier aunque el jugador esté parado encima, que es lo que ya resuelve
    /// <see cref="TileStandStatusProvider"/> para el otro lado del mismo incendio.
    /// </para>
    /// </remarks>
    public sealed class OwnedTilesStatusProvider : IStatusIconProvider
    {
        /// <summary>
        /// Key propia y no <c>status.burn</c>: "Quemadura" es el estado del que está parado en el
        /// fuego, y esta tarjeta habla de las casillas que el jefe mantiene ardiendo. Compartir la
        /// key era leer "el jefe se quema" donde lo que pasa es "el jefe quema".
        /// </summary>
        public const string FireTilesId = "enemy.fire_tiles";

        private readonly StatusIconCatalogSO _catalog;

        public OwnedTilesStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null || ownerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null) return;

            // Un incendio son muchas instancias y una sola tarjeta: el jugador no cuenta casillas,
            // pregunta si el piso quema. Se queda la que más dura, que es cuándo deja de quemar.
            SpecialTileDefinitionSO hottest = null;
            int longest = 0;

            foreach (var info in tiles.ActiveInstances())
            {
                if (info.OwnerGuid != ownerGuid) continue;

                var definition = info.Definition;
                if (definition == null) continue;
                if (definition.TileType != SpecialTileType.Fire &&
                    definition.TileType != SpecialTileType.FireTemp) continue;

                if (hottest != null && info.RemainingRounds <= longest) continue;
                hottest = definition;
                longest = info.RemainingRounds;
            }

            if (hottest == null) return;

            into.Add(FireTilesState(
                hottest,
                _catalog != null ? _catalog.Resolve(TileStandStatusProvider.BurnId) : null,
                remainingRounds: longest > 0 ? longest : (int?)null));
        }

        /// <summary>
        /// La tarjeta de las casillas ardiendo, con los números de ESA definición. Estática para
        /// que el preview de editor arme exactamente esta tarjeta y no una maqueta.
        /// </summary>
        public static StatusIconState FireTilesState(SpecialTileDefinitionSO definition,
                                                     UnityEngine.Sprite icon,
                                                     int? remainingRounds = null)
        {
            int enter = definition != null ? definition.EnterDamage : 0;
            int turnStart = definition != null ? definition.TurnStartDamage : 0;

            return new StatusIconState(
                FireTilesId,
                LocalizedContent.Name(FireTilesId, "Casillas de fuego"),
                LocalizedContent.DescriptionFormat(FireTilesId,
                    "<b>{0}</b> al entrar en una casilla. <b>{1}</b> si empezás tu turno sobre ella.",
                    enter, turnStart),
                icon,
                active: true,
                remainingTurns: remainingRounds,
                // Terrain y no Unit: habla del suelo, no del bicho. Con eso además la fila que
                // flota sobre su cabeza la saltea sola — el fuego está en el piso, y ahí se ve.
                style: StatusCardStyle.Terrain);
        }
    }
}
