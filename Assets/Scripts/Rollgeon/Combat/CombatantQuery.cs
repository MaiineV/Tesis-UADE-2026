using System;
using System.Linq;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Chests;
using Rollgeon.Combat.Rooms;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;

namespace Rollgeon.Combat
{
    /// <summary>
    /// Filtros compartidos de "quién es un enemigo elegible" para los ítems activos
    /// rediseñados (Feature#0085). <see cref="IEntityQueryService.GetAllEnemiesOf"/>
    /// devuelve TODO lo no-jugador — cofres y props de sala incluidos — así que cualquier
    /// consumidor que quiera "enemigos de verdad" necesita las mismas exclusiones que
    /// <c>ClassSkillPushResolver.Classify</c> ya resuelve para el choque del Empuje.
    /// </summary>
    public static class CombatantQuery
    {
        /// <summary>
        /// Enemigos VIVOS de <paramref name="player"/>: excluye cofres
        /// (<see cref="IChestRegistry"/>), objetos de sala rastreados
        /// (<see cref="IRoomObjectCleanupService"/>), HP ≤ 0 y entidades sin posición en
        /// grilla. Servicios faltantes en <see cref="ServiceLocator"/> ⇒ lista vacía (nunca
        /// excepción: un item que no encuentra pool simplemente no tiene a quién pegarle).
        /// </summary>
        public static List<Guid> LiveEnemiesOf(Guid player)
        {
            var result = new List<Guid>();
            if (player == Guid.Empty) return result;
            if (!ServiceLocator.TryGetService<IEntityQueryService>(out var query) || query == null)
                return result;

            ServiceLocator.TryGetService<IChestRegistry>(out var chests);
            ServiceLocator.TryGetService<IRoomObjectCleanupService>(out var roomObjects);
            ServiceLocator.TryGetService<AttributesManager>(out var attrs);
            ServiceLocator.TryGetService<IGridManager>(out var grid);

            foreach (var entity in query.GetAllEnemiesOf(player))
            {
                var guid = entity.Guid;
                if (guid == Guid.Empty) continue;

                if (chests != null && chests.IsChest(guid)) continue;
                if (roomObjects != null && roomObjects.Tracked.Contains(guid)) continue;

                if (attrs == null || !attrs.IsRegistered(guid)) continue;
                var hp = attrs.GetAttribute<Health>(guid);
                if (hp == null || hp.Value <= 0) continue;

                if (grid == null || !grid.TryGetPosition(guid, out _)) continue;

                result.Add(guid);
            }

            return result;
        }

        /// <summary>Elegible como fuente/target de Sangrado: vivo (implícito por el caller) y no <see cref="UnitTraits.Bloodless"/>.</summary>
        public static bool IsEligibleForBlood(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            return !GetTraits(entity).Bloodless;
        }

        /// <summary>
        /// Se puede desplazar por empuje/atracción/swap: no <see cref="UnitTraits.Immovable"/>,
        /// no jefe y footprint 1×1 (los jefes y multi-celda no se mueven por estas mecánicas).
        /// </summary>
        public static bool IsMovable(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            var traits = GetTraits(entity);
            if (traits.Immovable || traits.IsBoss) return false;

            if (ServiceLocator.TryGetService<IGridManager>(out var grid) && grid != null)
            {
                var footprint = grid.GetFootprint(entity);
                if (!GridFootprint.IsUnit(footprint)) return false;
            }

            return true;
        }

        /// <summary>Se puede aturdir: no <see cref="UnitTraits.StunImmune"/>.</summary>
        public static bool IsStunnable(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            return !GetTraits(entity).StunImmune;
        }

        /// <summary>HP actual, o 0 si la entidad no está registrada.</summary>
        public static int CurrentHp(Guid entity)
        {
            if (entity == Guid.Empty) return 0;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return 0;
            if (!attrs.IsRegistered(entity)) return 0;
            return attrs.GetAttribute<Health>(entity)?.Value ?? 0;
        }

        /// <summary>HP máximo resuelto vía <see cref="MaxHpResolver"/> (jefes/enemigos/jugador).</summary>
        public static int MaxHp(Guid entity) => MaxHpResolver.Resolve(entity);

        /// <summary>Coordenada actual en grilla, si está posicionada.</summary>
        public static bool TryGetCoord(Guid entity, out GridCoord coord)
        {
            coord = default;
            if (entity == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return false;
            return grid.TryGetPosition(entity, out coord);
        }

        /// <summary>Traits registrados, o el perfil seguro por default si nadie los registró.</summary>
        private static UnitTraits GetTraits(Guid entity)
        {
            if (ServiceLocator.TryGetService<IUnitTraitService>(out var traits) && traits != null)
                return traits.Get(entity);
            return UnitTraits.DefaultGround;
        }
    }
}
