using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Localization;
using Rollgeon.Tiles;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Publica los estados "parado sobre" (fuego, curación, impulso, fortaleza) del player en
    /// la fila de status icons. Sin turnos: aparecen al pisar la casilla y desaparecen al
    /// salir — la vista se refresca con los eventos de movimiento y de lifecycle de casillas.
    /// </summary>
    public sealed class TileStandStatusProvider : IStatusIconProvider
    {
        public const string BurnId = "status.burn";
        public const string HealId = "status.tile_heal";
        public const string SpeedId = "status.tile_speed";
        public const string AttackId = "status.tile_attack";

        private readonly StatusIconCatalogSO _catalog;

        // Reusada entre Collects: la fila se repinta en cada movimiento y esto corre en pleno
        // combate. No es cero allocs — CollectUnder copia las coords de cada instancia — pero
        // son una o dos casillas, y sin la definición no hay forma de saber cuánto cobra la que
        // estás pisando.
        private readonly List<SpecialTileInfo> _under = new();

        public TileStandStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null) return;

            tiles.CollectUnder(ownerGuid, _under);
            if (_under.Count == 0) return;

            // Fire y FireTemp colapsan en un solo "quemándose": para el jugador es el mismo
            // estado, y dos íconos idénticos leerían como bug.
            bool burnAdded = false;
            foreach (var info in _under)
            {
                var def = info.Definition;
                switch (def.TileType)
                {
                    case SpecialTileType.Fire:
                    case SpecialTileType.FireTemp:
                        if (burnAdded) break;
                        burnAdded = true;
                        into.Add(BurnState(def, _catalog != null ? _catalog.Resolve(BurnId) : null));
                        break;

                    case SpecialTileType.Heal:
                        Add(into, HealId, "Casilla de Curación",
                            "Terminá tu turno acá para recuperar vida.");
                        break;

                    case SpecialTileType.Boost:
                        Add(into, SpeedId, "Impulso",
                            "Esta casilla mejora tu próximo movimiento.");
                        break;

                    case SpecialTileType.Strength:
                        Add(into, AttackId, "Fortaleza",
                            "Tus combos ofensivos hacen daño extra mientras permanezcas acá.");
                        break;
                }
            }
        }

        /// <summary>
        /// El "quemándose" con los números de ESA casilla. Público y estático para que la tarjeta
        /// de suelo de un jefe diga la misma frase sin copiarla.
        /// </summary>
        /// <remarks>
        /// Los números salen de la definición y no de la key: cuatro fuegos comparten
        /// <see cref="SpecialTileType"/> y cobran 8/12, 6/10 y 15/15.
        /// </remarks>
        public static StatusIconState BurnState(SpecialTileDefinitionSO definition, UnityEngine.Sprite icon,
                                                StatusCardStyle style = StatusCardStyle.Unit)
        {
            int enter = definition != null ? definition.EnterDamage : 0;
            int turnStart = definition != null ? definition.TurnStartDamage : 0;

            return new StatusIconState(
                BurnId,
                LocalizedContent.Name(BurnId, "Quemándose"),
                LocalizedContent.DescriptionFormat(BurnId,
                    "<b>{0}</b> al entrar en una casilla. <b>{1}</b> si empezás tu turno sobre ella.",
                    enter, turnStart),
                icon,
                active: true,
                remainingTurns: null,
                stackCount: null,
                style: style);
        }

        private void Add(List<StatusIconState> into, string id, string fallbackName, string fallbackDesc)
        {
            into.Add(new StatusIconState(
                id,
                LocalizedContent.Name(id, fallbackName),
                LocalizedContent.Description(id, fallbackDesc),
                _catalog != null ? _catalog.Resolve(id) : null,
                active: true,
                remainingTurns: null));
        }
    }
}
