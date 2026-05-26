using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Readers
{
    /// <summary>
    /// Reader que devuelve el oro actual del jugador via <see cref="IEconomyService"/>.
    /// Permite a un trigger configurar bonus/costos dependientes del oro actual
    /// sin hardcodear (ej. "+1 daño por cada 10 oro" via composición con un
    /// multiplicador en el trigger).
    /// </summary>
    /// <remarks>
    /// Es compartido con el sistema de combat existente — es un
    /// <see cref="EffectIntReader"/> normal y se puede usar en cualquier
    /// <see cref="EffectContext"/>, no solo en enchantments.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadCurrentGold : EffectIntReader
    {
        public override int Read(EffectContext context)
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return 0;
            return economy.CurrentGold;
        }
    }
}
