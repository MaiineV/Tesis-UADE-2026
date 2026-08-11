using System;

namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Seam opcional del <see cref="DamagePipeline"/>: piso de HP por (target, source).
    /// Mismo patrón que <see cref="ILethalDamageOverride"/> (tutorial) — si no hay
    /// provider registrado el pipeline se comporta idéntico.
    /// <para>
    /// Consumidor actual: el cofre Mimic (GDD del Cofre §25) — daño de enemigos o
    /// eventos nunca lo deja por debajo de 1 HP; solo el contacto del jugador lo
    /// resuelve. El provider decide por source, el pipeline solo clampea.
    /// </para>
    /// </summary>
    public interface IMinHpClampProvider
    {
        /// <summary>
        /// <c>true</c> si el daño de <paramref name="sourceId"/> sobre
        /// <paramref name="targetId"/> debe respetar un piso de HP
        /// (<paramref name="minHp"/>). <c>false</c> = sin clamp.
        /// </summary>
        bool TryGetMinHp(Guid targetId, Guid sourceId, out int minHp);
    }
}
