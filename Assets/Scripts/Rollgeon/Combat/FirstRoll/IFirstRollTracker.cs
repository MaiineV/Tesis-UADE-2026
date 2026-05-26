using System;

namespace Rollgeon.Combat.FirstRoll
{
    /// <summary>
    /// Servicio que trackea si una entidad está procesando todavía la
    /// "primera tirada" del combate actual. Consumido por
    /// <c>PCFirstRollOfCombat</c> (TECHNICAL.md §8.2 + §6 Berserker pasiva).
    /// <para>
    /// El tracker se resetea en <c>OnCombatStart</c> y consume el flag por
    /// entidad cuando se observa el primer <c>OnRollResolved</c> de esa entidad.
    /// </para>
    /// </summary>
    public interface IFirstRollTracker
    {
        /// <summary>
        /// <c>true</c> si <paramref name="entityGuid"/> aún no resolvió ningún
        /// roll desde el último <c>OnCombatStart</c>. <c>false</c> fuera de combate.
        /// </summary>
        bool IsFirstRoll(Guid entityGuid);
    }
}
