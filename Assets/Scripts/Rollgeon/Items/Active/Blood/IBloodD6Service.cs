using System;

namespace Rollgeon.Items.Active.Blood
{
    /// <summary>
    /// Carga pendiente de Blood D6 (Feature#0084): arma el bonus del próximo combo de
    /// Ataque válido de <c>owner</c>. Combat-scoped: <see cref="ClearAll"/> se llama en
    /// <c>OnCombatEnd</c>/<c>OnRunEnd</c>.
    /// </summary>
    public interface IBloodD6Service
    {
        /// <summary>
        /// Arma la carga para <paramref name="owner"/> con la cara <paramref name="face"/>
        /// (1..6, clampeada). Bonus/receptores según tabla de cara del GDD. Pisa una carga
        /// previa si existiera (no debería pasar: gatear con <c>PcBloodD6Ready</c>).
        /// </summary>
        void Arm(Guid owner, int face);

        /// <summary><c>true</c> si <paramref name="owner"/> tiene una carga armada sin consumir.</summary>
        bool HasPending(Guid owner);

        /// <summary>
        /// <c>bonus%</c> de la carga pendiente de <paramref name="owner"/>, para el badge de
        /// HUD. <c>false</c> sin carga pendiente.
        /// </summary>
        bool TryGetPendingBonusPct(Guid owner, out int bonusPct);

        /// <summary>Descarta la carga (y cualquier combo en espera) de <paramref name="owner"/> sin consumirla.</summary>
        void Clear(Guid owner);

        /// <summary>Teardown de scope: limpia todo SIN eventos.</summary>
        void ClearAll();
    }
}
