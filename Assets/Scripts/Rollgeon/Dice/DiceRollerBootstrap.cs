using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Wrapper <see cref="ScriptableObject"/> que registra el
    /// <see cref="DiceRoller"/> en el <c>ServiceBootstrapSO.ExtraServices</c>.
    /// TECHNICAL.md §6.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> <see cref="ServiceScope.Run"/> per TECHNICAL.md líneas
    /// 359–360 — una instancia fresca por run garantiza una secuencia RNG
    /// independiente entre intentos.
    /// </para>
    /// <para>
    /// <b>Priority.</b> <c>72</c> — después de <c>RerollBudgetService</c> (70) por
    /// ordenamiento; el roller no consume otros servicios pero conviene quedar
    /// agrupado con el resto de servicios de dice.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Dice/Dice Roller Bootstrap",
        fileName = "DiceRollerBootstrap")]
    public sealed class DiceRollerBootstrap : ScriptableObject, IPreloadableService
    {
        private DiceRoller _instance;

        public int Priority => 72;
        public ServiceScope Scope => ServiceScope.Run;

        public void Register()
        {
            _instance = new DiceRoller();
            ServiceLocator.AddService<IDiceRoller>(_instance, ServiceScope.Run);
        }
    }
}
