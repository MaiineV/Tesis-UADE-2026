using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Effects.Selection;
using Rollgeon.Player;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Implementación de runtime de <see cref="IEntityQueryService"/> para el modelo de
    /// combate <b>1 player vs N enemigos</b>. Clasifica relaciones leyendo el roster vivo
    /// del <see cref="AttributesManager"/> (todas las entidades registradas) y la identidad
    /// del player del <see cref="IPlayerService"/>: el player es su propia facción; todo lo
    /// demás registrado es la facción enemiga.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué existía un hueco.</b> La interfaz y todos sus consumidores (selectores de
    /// IA <c>TargetSelector_*</c>, <see cref="Behaviors.SupportHealBehavior"/>,
    /// <c>PcAllyAliveExists</c>, <c>SelectionSettings</c>) se escribieron antes que esta
    /// implementación; sólo había stubs en tests. Sin un registro de runtime, los selectores
    /// de IA fallaban cerrado (<see cref="Guid.Empty"/>) y el healer nunca curaba — mientras
    /// la selección del player parecía andar por el fallback permisivo de
    /// <c>SelectionSettings</c>.
    /// </para>
    /// <para>
    /// <b>Relaciones.</b> Relativas al owner: misma facción → <see cref="EntityFilterMask.Allies"/>,
    /// distinta → <see cref="EntityFilterMask.Enemies"/>. El target player suma siempre el bit
    /// <see cref="EntityFilterMask.Player"/>, así un selector de enemigo configurado con
    /// <c>Player</c> o con <c>Enemies</c> lo encuentra igual.
    /// </para>
    /// <para>
    /// <b>Entity wrappers.</b> <see cref="GetAllAlliesOf"/> / <see cref="GetAllEnemiesOf"/>
    /// devuelven <see cref="Entity"/> envolviendo sólo el <see cref="Guid"/> — no existe un
    /// registro central guid→Entity y los únicos consumidores leen <c>.Guid</c>. Si más
    /// adelante se necesita el Entity real (con Passive bindeado), inyectar ese registro acá.
    /// </para>
    /// <para>
    /// <b>Limitación conocida.</b> No modela Neutrals/Props: cualquier entidad registrada que
    /// no sea el player se considera enemy-team. En la práctica el roster de combate es
    /// player + enemigos, y los consumidores filtran por Health, así que alcanza.
    /// </para>
    /// </remarks>
    public sealed class EntityQueryService : IEntityQueryService
    {
        /// <inheritdoc />
        public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) =>
            QueryByFaction(ownerGuid, sameFactionAsOwner: true);

        /// <inheritdoc />
        public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) =>
            QueryByFaction(ownerGuid, sameFactionAsOwner: false);

        /// <inheritdoc />
        public EntityFilterMask GetRelationship(Guid owner, Guid target)
        {
            if (!TryGetPlayerGuid(out var player))
            {
                // Sin player conocido no podemos clasificar — None es el neutro seguro
                // (ningún filtro matchea, igual que cuando el servicio no estaba registrado).
                return EntityFilterMask.None;
            }

            bool ownerIsPlayer = owner == player;
            bool targetIsPlayer = target == player;

            var mask = (ownerIsPlayer == targetIsPlayer)
                ? EntityFilterMask.Allies
                : EntityFilterMask.Enemies;

            if (targetIsPlayer)
                mask |= EntityFilterMask.Player;

            return mask;
        }

        // ------------------------------------------------------------------

        private static IEnumerable<Entity> QueryByFaction(Guid ownerGuid, bool sameFactionAsOwner)
        {
            var result = new List<Entity>();
            if (ownerGuid == Guid.Empty) return result;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
                return result;
            if (!TryGetPlayerGuid(out var player)) return result;

            bool ownerIsPlayer = ownerGuid == player;

            foreach (var kvp in attrs.EnumerateEntries())
            {
                var candidate = kvp.Key;
                if (candidate == Guid.Empty) continue;
                if (candidate == ownerGuid) continue; // el owner nunca es su propio aliado/enemigo.

                bool candidateIsPlayer = candidate == player;
                bool sameFaction = candidateIsPlayer == ownerIsPlayer;
                if (sameFaction == sameFactionAsOwner)
                    result.Add(new Entity { Guid = candidate });
            }

            return result;
        }

        private static bool TryGetPlayerGuid(out Guid player)
        {
            player = Guid.Empty;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null)
                return false;
            player = playerService.PlayerGuid;
            return player != Guid.Empty;
        }
    }
}
