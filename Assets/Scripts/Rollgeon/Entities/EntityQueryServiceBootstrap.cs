using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// SO wrapper que registra <see cref="EntityQueryService"/> como
    /// <see cref="IEntityQueryService"/> en el <see cref="ServiceLocator"/> bajo
    /// <see cref="ServiceScope.Global"/>. Sin esto, los selectores de IA y el
    /// <see cref="Behaviors.SupportHealBehavior"/> fallan cerrado ("IEntityQueryService not
    /// registered") y el enemigo healer nunca cura.
    /// </summary>
    /// <remarks>
    /// Scope Global (default): el servicio es stateless — resuelve <see cref="AttributesManager"/>
    /// e <see cref="Player.IPlayerService"/> en cada llamada, así que el orden de bootstrap no
    /// es crítico. Priority 90 sólo lo deja correr después de esos servicios por prolijidad.
    /// Agregar este asset a <c>ServiceBootstrapSO.ExtraServices</c> (docs/setup, §1.1.1).
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Entity Query Service Bootstrap",
        fileName = "EntityQueryServiceBootstrap")]
    public sealed class EntityQueryServiceBootstrap : ScriptableObject, IPreloadableService
    {
        private EntityQueryService _instance;

        public int Priority => 90;

        public void Register()
        {
            if (_instance != null) return;
            _instance = new EntityQueryService();
            ServiceLocator.AddService<IEntityQueryService>(_instance, ServiceScope.Global);
        }
    }
}
