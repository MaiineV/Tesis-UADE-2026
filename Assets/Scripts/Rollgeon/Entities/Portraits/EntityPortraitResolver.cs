using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Entities.Portraits
{
    /// <summary>
    /// Impl in-memory de <see cref="IEntityPortraitResolver"/>: dict guid → sprite
    /// para entidades spawneadas + resolución lazy del player vía
    /// <see cref="IPlayerService"/> (su guid nunca se registra explícitamente).
    /// </summary>
    /// <remarks>
    /// Precedencia en <see cref="TryGetPortrait"/>: dict explícito primero, lazy
    /// player después. Así un futuro override por-run (skins, disfraces) puede
    /// pisar el portrait de la clase sin tocar el SO.
    /// <para>
    /// El sprite del player sale de <c>ClassHeroSO.ResolveTurnOrderIcon()</c>, no de
    /// su <c>Portrait</c> directo: el retrato de selección es grande y con fondo, y
    /// encogido a un slot de turno se lee mal. Los enemigos no tienen esa distinción.
    /// </para>
    /// </remarks>
    public sealed class EntityPortraitResolver : IEntityPortraitResolver
    {
        private readonly Dictionary<Guid, Sprite> _portraits = new Dictionary<Guid, Sprite>();
        private readonly IPlayerService _playerService;

        public EntityPortraitResolver(IPlayerService playerService = null)
        {
            _playerService = playerService;
        }

        /// <summary>
        /// Factory: resuelve <see cref="IPlayerService"/> del <see cref="ServiceLocator"/>
        /// (tolerante — puede faltar en tests) y registra la instancia como
        /// <see cref="IEntityPortraitResolver"/> en <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static EntityPortraitResolver CreateAndRegister()
        {
            ServiceLocator.TryGetService<IPlayerService>(out var playerService);
            var resolver = new EntityPortraitResolver(playerService);
            ServiceLocator.AddService<IEntityPortraitResolver>(resolver, ServiceScope.Run);
            return resolver;
        }

        public void Register(Guid entityId, Sprite portrait)
        {
            if (entityId == Guid.Empty || portrait == null) return;
            _portraits[entityId] = portrait;
        }

        public void Unregister(Guid entityId)
        {
            _portraits.Remove(entityId);
        }

        public bool TryGetPortrait(Guid entityId, out Sprite portrait)
        {
            if (_portraits.TryGetValue(entityId, out portrait) && portrait != null)
            {
                return true;
            }

            if (_playerService != null
                && entityId != Guid.Empty
                && entityId == _playerService.PlayerGuid)
            {
                // El heroe usa su icono de turnos (con fallback al retrato); los enemigos
                // y jefes siguen con su Portrait, que registran ellos al spawnear.
                var heroIcon = _playerService.CurrentHero != null
                    ? _playerService.CurrentHero.ResolveTurnOrderIcon()
                    : null;
                if (heroIcon != null)
                {
                    portrait = heroIcon;
                    return true;
                }
            }

            portrait = null;
            return false;
        }

        public void Clear()
        {
            _portraits.Clear();
        }
    }
}
