using System.Collections.Generic;

namespace Rollgeon.Combat.DiceBlock
{
    /// <summary>
    /// Bloqueo de dados individuales por turno (Sistemas prerequisito Bosses §2). Marca dados de
    /// la build (por índice de slot, 0..N-1) como no-disponibles para la resolución del turno
    /// actual: no entran a ningún combo y no se pueden re-rollear. Lo usa el Boss 1 (Contador de
    /// Pisos) para bloquear 1 dado (Fase 1) o 2 (Fase 2) por turno.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Índice de slot.</b> El índice es posicional y estable: corresponde al slot del dado en
    /// la <c>DiceBagSO</c> y a la posición en el array de caras (<c>int[5]</c>) que produce el roll.
    /// </para>
    /// <para>
    /// <b>Auto-release.</b> Se limpia al finalizar el turno del jugador (<c>OnTurnFinished</c> del
    /// player) — DoD §2. El Boss vuelve a sortear al final de su turno (decisión de diseño:
    /// el boss computa el bloqueo al cerrar su turno).
    /// </para>
    /// </remarks>
    public interface IDiceBlockService
    {
        /// <summary>Bloquea el dado en <paramref name="index"/>. No-op si <paramref name="index"/> &lt; 0.</summary>
        void Block(int index);

        /// <summary>Desbloquea un dado puntual. No-op si no estaba bloqueado.</summary>
        void Unblock(int index);

        /// <summary><c>true</c> si el dado en <paramref name="index"/> está bloqueado este turno.</summary>
        bool IsBlocked(int index);

        /// <summary>Índices bloqueados actualmente (vista read-only para UI / tests).</summary>
        IReadOnlyCollection<int> BlockedIndices { get; }

        /// <summary>Libera todos los bloqueos. Disparado al fin del turno del jugador y en OnCombatEnd/OnRunEnd.</summary>
        void Clear();
    }
}
