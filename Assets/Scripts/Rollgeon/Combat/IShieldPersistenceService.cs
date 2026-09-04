using System;

namespace Rollgeon.Combat
{
    /// <summary>
    /// Flag de "este escudo sobrevive al próximo reset" (Feature#0085, Coin Shield cara
    /// par: el jugador salta el reset de <see cref="ShieldResetHandler"/> una única vez).
    /// Combat-scoped: <see cref="ClearAll"/> se llama en <c>OnCombatEnd</c>.
    /// </summary>
    public interface IShieldPersistenceService
    {
        /// <summary>Marca a <paramref name="entity"/> para saltear su PRÓXIMO reset de escudo por inicio de turno.</summary>
        void PersistThroughNextReset(Guid entity);

        /// <summary>
        /// Consume la marca de persistencia si existía. <c>true</c> ⇒ el caller
        /// (<see cref="ShieldResetHandler"/>) debe saltear el reset de este turno.
        /// </summary>
        bool TryConsume(Guid entity);

        /// <summary><c>true</c> si <paramref name="entity"/> tiene la marca activa sin consumir.</summary>
        bool IsPersisted(Guid entity);

        /// <summary>Teardown de scope: limpia todo SIN eventos.</summary>
        void ClearAll();
    }
}
