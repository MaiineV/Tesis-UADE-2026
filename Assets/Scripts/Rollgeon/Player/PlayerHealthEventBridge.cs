using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Player
{
    /// <summary>
    /// Puente vida→bus: re-emite <see cref="EventName.OnPlayerHealthChanged"/>
    /// <c>[playerGuid, current, max]</c> cada vez que la vida del JUGADOR cambia.
    /// </summary>
    /// <remarks>
    /// <c>OnPlayerHealthChanged</c> existía en el enum pero nadie lo emitía — el
    /// <c>ItemTriggerCatalog</c> lo excluía por eso. La vida viaja por
    /// <c>TypedEvent&lt;DamageResolvedPayload&gt;</c>/<c>&lt;HealResolvedPayload&gt;</c>,
    /// a los que un hook de ítem no llega. Este bridge escucha los DOS payloads (los
    /// levantan DamagePipeline, HealPipeline y <c>EffModifyIntAttribute.RaiseHealthDelta</c>,
    /// o sea todos los caminos reales de escritura de Health) y publica el evento legacy
    /// solo para el jugador — lo que un ítem tipo "al cruzar 30% HP" necesita escuchar.
    /// </remarks>
    public sealed class PlayerHealthEventBridge : IPreloadableService, IDisposable
    {
        private Action<DamageResolvedPayload> _onDamage;
        private Action<HealResolvedPayload> _onHeal;

        /// <summary>Después de AttributesManager/pipelines; sin dependientes de orden propios.</summary>
        public int Priority => 55;

        public void Register()
        {
            if (_onDamage != null) return;
            ServiceLocator.AddService<PlayerHealthEventBridge>(this, ServiceScope.Global);
            _onDamage = p => Emit(p.TargetGuid);
            _onHeal = p => Emit(p.TargetGuid);
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamage);
            TypedEvent<HealResolvedPayload>.Subscribe(_onHeal);
        }

        public void Dispose()
        {
            if (_onDamage != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamage);
                _onDamage = null;
            }
            if (_onHeal != null)
            {
                TypedEvent<HealResolvedPayload>.Unsubscribe(_onHeal);
                _onHeal = null;
            }
        }

        private static void Emit(Guid targetGuid)
        {
            if (targetGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null) return;
            if (ps.PlayerGuid != targetGuid) return;

            int current = 0;
            if (ServiceLocator.TryGetService<AttributesManager>(out var attrs) && attrs != null)
                current = attrs.GetAttributeValue<Health, int>(targetGuid);

            EventManager.Trigger(EventName.OnPlayerHealthChanged,
                targetGuid, current, PlayerMaxHp.Resolve(targetGuid));
        }
    }
}
