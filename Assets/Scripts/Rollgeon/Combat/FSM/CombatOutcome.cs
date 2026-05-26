using System;

namespace Rollgeon.Combat.FSM
{
    /// <summary>
    /// Resultado final de un combate. Payload del evento
    /// <c>EventName.OnCombatEnd</c> (args: <c>[Guid roomInstanceId, CombatOutcome outcome]</c>).
    /// </summary>
    /// <remarks>
    /// Plan §4 R4: este worktree define el enum. Si downstream (T103 Boss, etc.)
    /// quiere moverlo a <c>Rollgeon.Combat</c> raiz, es un refactor de 1 archivo.
    /// </remarks>
    [Serializable]
    public enum CombatOutcome
    {
        /// <summary>Sentinel — nunca se dispara como outcome real. Default de <c>CombatOutcome?</c> via <c>GetValueOrDefault()</c>.</summary>
        None = 0,
        /// <summary>El player venci a todos los enemigos.</summary>
        Victory = 1,
        /// <summary>El player fue derrotado.</summary>
        Defeat = 2,
        /// <summary>Combat abortado (escape, cancel, quit mid-combat).</summary>
        Aborted = 3,
    }
}
