using Patterns;
using Rollgeon.Dice;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que decora el <see cref="IDiceRoller"/>
    /// registrado por <c>DiceRollerBootstrap</c> con un <see cref="EnchantedDiceRoller"/>
    /// que respeta los face filters de los encantamientos activos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> Run — re-registra el decorator al inicio de cada run, tras
    /// <c>DiceRollerBootstrap</c> (que registra el roller base en Run scope con
    /// priority 72). Tras <c>ClearScope(Run)</c>, el decorator desaparece y el
    /// siguiente run recreará el wrapper.
    /// </para>
    /// <para>
    /// <b>Priority.</b> 80 — después del base roller (72) para garantizar que el
    /// <c>ServiceLocator.GetService&lt;IDiceRoller&gt;</c> devuelva el inner antes
    /// de envolverlo.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Upgrades/Dice/Enchantment Roller Bootstrap",
        fileName = "EnchantmentRollerBootstrap")]
    public sealed class EnchantmentRollerBootstrap : ScriptableObject, IPreloadableService
    {
        public int Priority => 80;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            if (!ServiceLocator.TryGetService<IDiceRoller>(out var inner) || inner == null)
            {
                Debug.LogWarning("[EnchantmentRollerBootstrap] IDiceRoller no registrado todavía — " +
                                 "el decorator no se aplica. Verificar que DiceRollerBootstrap corre antes (priority < 80).");
                return;
            }

            var decorator = new EnchantedDiceRoller(inner);
            ServiceLocator.AddService<IDiceRoller>(decorator, ServiceScope.Run);
        }
    }
}
