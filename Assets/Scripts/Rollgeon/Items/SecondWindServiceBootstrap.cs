using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> para <c>ServiceBootstrapSO.ExtraServices</c>.
    /// Run-scope, Priority 65 (después de InventoryService 60, al que consulta).
    /// Registra <see cref="SecondWindService"/> como el <see cref="ILethalDamageOverride"/>
    /// de la run; en el tutorial, <c>TutorialInvulnerabilityService</c> se registra
    /// después bajo la misma key y lo pisa — ahí no existen items.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Items/Second Wind Service Bootstrap",
        fileName = "SecondWindServiceBootstrap")]
    public sealed class SecondWindServiceBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 65;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            ServiceLocator.AddService<ILethalDamageOverride>(new SecondWindService(), ServiceScope.Run);
        }
    }
}
