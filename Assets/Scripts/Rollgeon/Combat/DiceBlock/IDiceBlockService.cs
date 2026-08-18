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
        /// <param name="label">
        /// Qué se llevó el dado, para escribirlo sobre el candado (el Croupier pasa el número que
        /// cantó). <c>null</c> ⇒ candado pelado, que es lo que muestran los jefes que lo sortean al
        /// azar: ahí no hay nada que explicar.
        /// </param>
        void Block(int index, string label = null);

        /// <summary>
        /// Etiqueta del bloqueo en <paramref name="index"/>, o <c>null</c> si no tiene o no está
        /// bloqueado.
        /// </summary>
        /// <remarks>
        /// Existe para que la UI pueda decir <b>por qué</b> se fue el dado. Con el Croupier, el
        /// número que canta la ruleta es a la vez el sector que detona y el dado que confisca, y sin
        /// esta etiqueta las dos mitades de esa frase no se tocan en pantalla.
        /// <para>
        /// <b>No es la posición del dado.</b> El índice sale de <c>número % dados</c>, así que con
        /// seis sectores y cinco dados el 6 confisca el primero. La etiqueta dice <i>quién</i> se lo
        /// llevó; <i>cuál</i> ya lo marca el candado.
        /// </para>
        /// </remarks>
        string LabelOf(int index);

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
