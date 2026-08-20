namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Bono plano al daño saliente de un ataque ofensivo, resuelto por atacante en el
    /// momento del golpe. Primer consumidor: la casilla Fortaleza (+10 al combo mientras
    /// la unidad permanezca sobre ella).
    /// </summary>
    /// <remarks>
    /// El pipeline lo consulta solo para <see cref="AttackKind.ComboAttack"/> y
    /// <see cref="AttackKind.BasicAttack"/> — estructuralmente ofensivo: los DoT, reacciones
    /// y daño ambiental no se buffean por posición. On-demand a propósito: el bono aparece
    /// al pararse en la casilla y desaparece al abandonarla sin ningún bookkeeping.
    /// </remarks>
    public interface IOutgoingFlatDamageBonusProvider
    {
        /// <summary>Bono plano para este golpe (0 = sin bono).</summary>
        int GetFlatBonus(DamageContext ctx);
    }
}
