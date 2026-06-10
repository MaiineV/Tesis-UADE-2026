using System.Collections.Generic;

namespace Rollgeon.Meta
{
    /// <summary>
    /// Servicio global de meta-progresión (#164). Fuente de verdad de qué
    /// elementos están desbloqueados y de los contadores persistentes entre runs.
    /// Los pools (dados, clases, shop, encantamientos, salas) consultan
    /// <see cref="IsAvailable"/> — idealmente vía <see cref="MetaUnlockGate"/>,
    /// que degrada a "todo disponible" cuando el servicio no está registrado.
    /// </summary>
    public interface IMetaProgressionService
    {
        /// <summary>
        /// <c>true</c> si el elemento puede ofrecerse al jugador: fue desbloqueado
        /// explícitamente, o ningún <see cref="UnlockDefinitionSO"/> lo gatea
        /// (pool base).
        /// </summary>
        bool IsAvailable(UnlockableCategory category, string targetId);

        /// <summary><c>true</c> si la definición ya fue cumplida (para la pantalla de desbloqueos).</summary>
        bool IsDefinitionCompleted(UnlockDefinitionSO definition);

        /// <summary>Todas las definiciones del catálogo (no-null), para tools y UI.</summary>
        IReadOnlyList<UnlockDefinitionSO> Definitions { get; }

        /// <summary>
        /// Marca la definición como cumplida, desbloquea su target, persiste a disco
        /// y dispara <c>TypedEvent&lt;UnlockAchievedPayload&gt;</c>. Idempotente:
        /// devuelve <c>false</c> si ya estaba cumplida.
        /// </summary>
        bool TryUnlock(UnlockDefinitionSO definition, bool duringRun);

        /// <summary>Racha actual de runs ganadas consecutivas.</summary>
        int ConsecutiveWins { get; }

        /// <summary>Clases distintas jugadas entre runs (contador de acumulación).</summary>
        IReadOnlyCollection<string> ClassesPlayed { get; }

        /// <summary>
        /// Registra el cierre de una run: suma la clase al set persistente y
        /// actualiza la racha (gana → +1; muere → 0). Persiste a disco.
        /// </summary>
        void RecordRunCompleted(bool won, string classId);

        /// <summary>Fuerza un write-through del estado actual.</summary>
        void SaveNow();
    }
}
