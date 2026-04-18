using System;

namespace Rollgeon.Combat.Weakness
{
    /// <summary>
    /// Servicio que, dado un <paramref name="target"/> y el <c>ComboId</c> matcheado
    /// por el atacante, devuelve el multiplicador de weakness a aplicar al damage.
    /// <para>
    /// <b>Contrato.</b>
    /// <list type="bullet">
    ///   <item>Retorna <c>1.0f</c> cuando no hay weakness (target desconocido, combo no
    ///     matchea la debilidad, comboId null/empty, o <c>target == Guid.Empty</c>).</item>
    ///   <item>Retorna el multiplicador (&gt; 1.0f) cuando el combo coincide con la
    ///     debilidad del target. En ese caso la implementacion <b>dispara</b>
    ///     <see cref="Patterns.EventName.OnWeaknessHit"/> via <see cref="Patterns.EventManager"/>
    ///     con <c>args = [attackerGuid, targetGuid]</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IWeaknessChecker
    {
        /// <summary>Ver contract en el type summary. Default behavior: return 1.0f.</summary>
        float GetMultiplier(Guid attacker, Guid target, string matchedComboId);
    }
}
