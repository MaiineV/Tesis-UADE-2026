using System.Collections.Generic;
using Rollgeon.Dice;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Filtra el set de caras válidas de un dado encantado <i>antes</i> del roll.
    /// Implementado por concretos polimórficos (<c>OnlyEvens</c>, <c>OnlyPrimes</c>,
    /// <c>FaceRange</c>, etc.) creados por los diseñadores via Odin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Composición por intersección.</b> Cuando un dado tiene varios encantamientos
    /// con filter, el roller los aplica en cascada: <c>allowed = filter₁(filter₀(initial))</c>.
    /// La validación de la sala (Phase 4) impide aplicar un filter que dejaría la
    /// intersección vacía.
    /// </para>
    /// <para>
    /// <b>Pre-roll only.</b> El filter no ve el resultado del dado — opera solo sobre
    /// el set de valores posibles. Para reaccionar al resultado (ej. bonus si sale par),
    /// usar un <c>IOnDiceRolledTrigger</c>.
    /// </para>
    /// </remarks>
    public interface IFaceFilter
    {
        /// <summary>
        /// Devuelve el subconjunto de <paramref name="currentlyAllowed"/> que este
        /// filter permite para un dado de tipo <paramref name="type"/>. Debe ser
        /// pure (no side effects) para poder llamarse en validación previa al apply.
        /// </summary>
        IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed);
    }
}
