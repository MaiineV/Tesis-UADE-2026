using System.Collections.Generic;
using Patterns.Save;

namespace Rollgeon.Meta
{
    /// <summary>
    /// POCO con el estado persistente de meta-progresión (#164): elementos
    /// desbloqueados por categoría y contadores entre runs. Implementa
    /// <see cref="ISaveable"/> (§15) con key <c>"meta.progression"</c>; a diferencia
    /// de los states run-scoped, este vive en <c>ServiceScope.Global</c> dentro del
    /// <see cref="MetaProgressionService"/> y se respalda a disco vía
    /// <see cref="IMetaSaveStore"/> en cada mutación.
    /// </summary>
    public sealed class MetaProgressionState : ISaveable
    {
        public const string SaveKeyConst = "meta.progression";

        /// <summary>Claves <c>categoría:targetId</c> desbloqueadas explícitamente.</summary>
        public HashSet<string> UnlockedTargetKeys = new HashSet<string>();

        /// <summary><c>UnlockId</c>s de definiciones ya cumplidas.</summary>
        public HashSet<string> CompletedUnlockIds = new HashSet<string>();

        /// <summary>Racha de runs ganadas consecutivas (consistencia — resetea al morir).</summary>
        public int ConsecutiveWins;

        /// <summary>Clases distintas jugadas entre runs (acumulación — nunca resetea).</summary>
        public HashSet<string> ClassesPlayed = new HashSet<string>();

        // ---------------------------------------------------------------- ISaveable

        /// <inheritdoc />
        public string SaveKey => SaveKeyConst;

        /// <inheritdoc />
        public object CaptureState()
        {
            return new MetaProgressionSnapshot
            {
                UnlockedTargetKeys = new List<string>(UnlockedTargetKeys),
                CompletedUnlockIds = new List<string>(CompletedUnlockIds),
                ConsecutiveWins = ConsecutiveWins,
                ClassesPlayed = new List<string>(ClassesPlayed),
            };
        }

        /// <inheritdoc />
        public void RestoreState(object state)
        {
            UnlockedTargetKeys.Clear();
            CompletedUnlockIds.Clear();
            ClassesPlayed.Clear();
            ConsecutiveWins = 0;

            if (state is not MetaProgressionSnapshot snapshot) return;

            if (snapshot.UnlockedTargetKeys != null)
            {
                foreach (var key in snapshot.UnlockedTargetKeys)
                {
                    if (!string.IsNullOrEmpty(key)) UnlockedTargetKeys.Add(key);
                }
            }

            if (snapshot.CompletedUnlockIds != null)
            {
                foreach (var id in snapshot.CompletedUnlockIds)
                {
                    if (!string.IsNullOrEmpty(id)) CompletedUnlockIds.Add(id);
                }
            }

            if (snapshot.ClassesPlayed != null)
            {
                foreach (var classId in snapshot.ClassesPlayed)
                {
                    if (!string.IsNullOrEmpty(classId)) ClassesPlayed.Add(classId);
                }
            }

            ConsecutiveWins = snapshot.ConsecutiveWins;
        }
    }
}
