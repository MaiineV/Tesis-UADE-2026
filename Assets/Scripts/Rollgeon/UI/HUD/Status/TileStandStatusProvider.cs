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

        // Reusada entre Collects: la fila se repinta en cada movimiento y esto corre en
        // pleno combate — cero allocs por refresh.
        private readonly List<SpecialTileType> _typesScratch = new();

        public TileStandStatusProvider(StatusIconCatalogSO catalog) => _catalog = catalog;

        public void Collect(Guid ownerGuid, List<StatusIconState> into)
        {
            if (into == null) return;
            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null) return;

            tiles.CollectTypesUnder(ownerGuid, _typesScratch);
            if (_typesScratch.Count == 0) return;

            // Fire y FireTemp colapsan en un solo "quemándose": para el jugador es el mismo
            // estado, y dos íconos idénticos leerían como bug.
            bool burnAdded = false;
            foreach (var type in _typesScratch)
            {
                switch (type)
                {
                    case SpecialTileType.Fire:
                    case SpecialTileType.FireTemp:
                        if (burnAdded) break;
                        burnAdded = true;
                        Add(into, BurnId, "Quemándose",
                            "Estás sobre Fuego: recibís daño al inicio de tu turno mientras sigas acá.");
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
