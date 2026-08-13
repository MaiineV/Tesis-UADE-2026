using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combos.Play
{
    /// <summary>
    /// Bootstrap de la ventana de combo jugado — crea y registra el <see cref="ComboPlayService"/>.
    /// </summary>
    /// <remarks>
    /// <b>Priority.</b> 87 — después de <c>DiceEnchantmentBootstrap</c> (85) y
    /// <c>ComboPassiveBootstrap</c> (86): los services que escuchan <c>ComboPlayedPayload</c>
    /// ya están registrados cuando este se levanta.
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Combos/Combo Play Bootstrap",
        fileName = "ComboPlayBootstrap")]
    public sealed class ComboPlayBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 87;
        public ServiceScope Scope => ServiceScope.Global;

        public void Register()
        {
            var service = new ComboPlayService();
            service.Register();
        }
    }
}
