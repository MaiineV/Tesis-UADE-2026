using System;

namespace Rollgeon.Combat
{
    /// <summary>
    /// Traduce el <c>SourceId</c> de un golpe letal al Guid que recibe el crédito de la kill.
    /// Regla del GDD de Casillas Especiales: una muerte causada por una casilla (o por un
    /// estado que ella aplicó, ej. veneno) se acredita SIEMPRE al player, aunque el
    /// <c>DamageContext.SourceId</c> siga siendo el instanceId de la casilla — ese id se
    /// conserva para trazabilidad y para que el visual no rote al player hacia cada víctima.
    /// </summary>
    public interface IKillCreditResolver
    {
        /// <summary>
        /// <c>true</c> si <paramref name="sourceId"/> es una fuente cuyo crédito se redirige
        /// (una casilla especial); <paramref name="credit"/> trae el Guid acreditado.
        /// </summary>
        bool TryResolveCredit(Guid sourceId, out Guid credit);
    }
}
