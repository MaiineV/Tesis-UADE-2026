using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Entities.Bosses
{
    /// <summary>
    /// Pool de bosses de un piso, roleado al entrar a la sala con el RNG determinístico por sala
    /// (seed derivada del <c>roomInstanceId</c>) ⇒ el boss elegido es estable por seed/run.
    /// </summary>
    /// <remarks>
    /// <b>Invariante ≥1 boss activo</b>: un piso sin boss no es jugable, así que si ninguna entry
    /// quedó elegible pero hay entries autoradas, <see cref="Roll"/> devuelve la primera no-nula con
    /// un warning. El <c>null</c> se reserva para "pool vacío / sin autorar".
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Entities/Bosses/Boss Pool",
        fileName = "BossPool")]
    public sealed class BossPoolSO : SerializedScriptableObject
    {
        [Title("Entries")]
        [InfoBox("Pool pesado del piso. Se rolea una vez al entrar a la sala de boss. Los pesos " +
                 "son relativos (2 vs 1 = el doble de chance). Para desactivar un boss sin borrar " +
                 "la entry: Weight = 0 o Enabled = off. Tiene que quedar al menos uno activo.")]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        [OdinSerialize]
        public List<WeightedBoss> Entries = new List<WeightedBoss>();

        /// <summary><c>null</c> solo si el pool no tiene ninguna entry con boss autorado — el caller cae a su path de spawn normal.</summary>
        public EnemyDataSO Roll(System.Random rng) => RollEntry(rng)?.Boss;

        /// <summary>Igual que <see cref="Roll"/> pero devuelve la entry entera: el boss y su sala salen del mismo sorteo, así no hay dos que puedan desincronizarse.</summary>
        public WeightedBoss RollEntry(System.Random rng)
        {
            if (Entries == null || Entries.Count == 0) return null;

            var picked = TryRollActive(rng);
            if (picked != null) return picked;

            // Invariante ≥1: preferimos un boss "mal configurado" a una sala vacía.
            var fallback = FirstAuthoredEntry();
            if (fallback != null)
            {
                Debug.LogWarning(
                    $"[BossPoolSO] '{name}': no active bosses, first entry will be used as fallback " +
                    $"('{fallback.Boss.EntityId}'). Revisá Weight/Enabled de las entries.");
            }
            return fallback;
        }

        /// <summary>Bosses que hoy pueden salir del roll, en orden de autorado.</summary>
        public IReadOnlyList<EnemyDataSO> ActiveBosses()
        {
            var result = new List<EnemyDataSO>();
            if (Entries == null) return result;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (IsActive(Entries[i])) result.Add(Entries[i].Boss);
            }
            return result;
        }

        /// <summary>Tiene boss, está <c>Enabled</c> y su peso es positivo: las dos palancas de apagado se chequean acá, en un solo lugar.</summary>
        public static bool IsActive(WeightedBoss entry)
        {
            if (entry == null || entry.Boss == null) return false;
            if (!entry.Enabled) return false;
            if (entry.Weight <= 0f) return false;
            return true;
        }

        private WeightedBoss TryRollActive(System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsActive(Entries[i])) continue;
                total += Entries[i].Weight;
            }
            if (total <= 0f) return null;

            // rng null solo en callers defensivos: la primera activa, y no UnityEngine.Random, que
            // rompería el determinismo por sala.
            float pick = (float)(rng != null ? rng.NextDouble() : 0d) * total;
            float cursor = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsActive(Entries[i])) continue;
                cursor += Entries[i].Weight;
                if (pick <= cursor) return Entries[i];
            }

            // Floating point drift — fallback a la última activa.
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (IsActive(Entries[i])) return Entries[i];
            }
            return null;
        }

        private WeightedBoss FirstAuthoredEntry()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i]?.Boss != null) return Entries[i];
            }
            return null;
        }

        private void OnValidate()
        {
            if (Entries == null) return;

            bool anyActive = false;
            bool anyAuthored = false;
            for (int i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null) continue;
                if (entry.Weight < 0f) entry.Weight = 0f;
                if (entry.Boss != null) anyAuthored = true;
                if (IsActive(entry)) anyActive = true;
            }

            if (anyAuthored && !anyActive)
            {
                Debug.LogWarning(
                    $"[BossPoolSO] '{name}': no active bosses, first entry will be used as fallback. " +
                    "Cada piso necesita al menos un boss con Enabled = on y Weight > 0.");
            }
        }
    }
}
