using System;
using Rollgeon.Effects.Readers;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Registro de overrides del término <c>dmg_base_PJ</c> de la fórmula N×M
    /// (<see cref="PlayerComboDamage.Resolve"/>). Para la categoría de items que
    /// "redefinen el daño base" (GDD): Furia Contenida, Egoísta.
    /// </summary>
    /// <remarks>
    /// NO es un modifier sobre Attack a propósito: un <c>Override</c> en Attack pisaría
    /// también <c>bonos_PJ</c> (los +Attack de otros items) y el GDD dice "el Daño Base
    /// pasa a 0", no "el Attack entero". El valor es un <see cref="EffectIntReader"/>
    /// evaluado en cada resolución — dinámico gratis (racha de Furia, oro de Egoísta).
    /// </remarks>
    public interface IBaseDamageOverrideService
    {
        /// <summary>
        /// Registra (o reemplaza) el override de <paramref name="sourceId"/> (ItemId).
        /// Con más de uno registrado gana el de mayor <paramref name="priority"/>
        /// (empate: el último) — la exclusión de compra entre items de la categoría es
        /// un problema de la tienda, esto es la red de seguridad.
        /// </summary>
        void Register(string sourceId, EffectIntReader baseValue, int priority);

        void Unregister(string sourceId);

        /// <summary>Hay al menos un override activo (HUD/debug).</summary>
        bool HasOverride { get; }

        /// <summary>
        /// Evalúa el reader ganador con un contexto mínimo (<c>SourceGuid = playerGuid</c>).
        /// <c>false</c> si no hay overrides. Float: las fracciones (Furia Contenida
        /// 0.25/ronda) viajan enteras hasta el redondeo único de la fórmula N×M.
        /// </summary>
        bool TryGetBaseDamage(Guid playerGuid, out float value);
    }
}
