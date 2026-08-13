using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Entities.Bosses
{
    /// <summary>
    /// Pool pesado de bosses de un piso. La sala de boss rolea contra este pool al
    /// entrar, con el RNG determinístico por sala (seed derivada del
    /// <c>roomInstanceId</c>) ⇒ el boss elegido es estable por seed/run.
    /// Mismo patrón de roulette wheel que <c>EnchantmentPoolSO</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sin MinFloorDepth.</b> A diferencia del pool de encantamientos, acá el piso ya
    /// está implícito: cada <c>FloorLayoutSO</c> apunta a su propio pool.
    /// </para>
    /// <para>
    /// <b>Invariante ≥1 boss activo.</b> Un piso sin boss no es jugable (la sala queda
    /// vacía y la run no se puede cerrar). Si ninguna entry quedó elegible pero hay
    /// entries autoradas, <see cref="Roll"/> devuelve la primera no-nula con un warning en
    /// vez de <c>null</c>. El <c>null</c> se reserva para "pool vacío / sin autorar", que
    /// el resolver interpreta como "usá el path de spawn de siempre".
    /// </para>
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

        /// <summary>
        /// Rolea un boss del pool. <c>null</c> solo si el pool no tiene ninguna entry con
        /// boss autorado — el caller (resolver) cae a su path de spawn normal.
        /// </summary>
        /// <param name="rng">RNG inyectable para determinismo por sala y tests.</param>
        public EnemyDataSO Roll(System.Random rng)
        {
            if (Entries == null || Entries.Count == 0) return null;

            var picked = TryRollActive(rng);
            if (picked != null) return picked;

            // Invariante ≥1: el piso necesita un boss aunque el autorado haya quedado
            // todo apagado. Preferimos un boss "mal configurado" a una sala vacía.
            var fallback = FirstAuthoredBoss();
            if (fallback != null)
            {
                Debug.LogWarning(
                    $"[BossPoolSO] '{name}': no active bosses, first entry will be used as fallback " +
                    $"('{fallback.EntityId}'). Revisá Weight/Enabled de las entries.");
            }
            return fallback;
        }

        /// <summary>
        /// Bosses que hoy pueden salir del roll, en orden de autorado. Consumido por el
        /// comando <c>boss list</c> de la dev console y por los tests.
        /// </summary>
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

        /// <summary>
        /// <c>true</c> si la entry entra al roll: tiene boss, está <c>Enabled</c> y su peso
        /// es positivo. Las dos palancas de apagado se chequean acá, en un solo lugar.
        /// </summary>
        public static bool IsActive(WeightedBoss entry)
        {
            if (entry == null || entry.Boss == null) return false;
            if (!entry.Enabled) return false;
            if (entry.Weight <= 0f) return false;
            return true;
        }

        private EnemyDataSO TryRollActive(System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsActive(Entries[i])) continue;
                total += Entries[i].Weight;
            }
            if (total <= 0f) return null;

            // rng null solo en callers defensivos: elegimos la primera activa en vez de
            // caer en UnityEngine.Random, que rompería el determinismo por sala.
            float pick = (float)(rng != null ? rng.NextDouble() : 0d) * total;
            float cursor = 0f;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (!IsActive(Entries[i])) continue;
                cursor += Entries[i].Weight;
                if (pick <= cursor) return Entries[i].Boss;
            }

            // Floating point drift — fallback a la última activa.
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (IsActive(Entries[i])) return Entries[i].Boss;
            }
            return null;
        }

        private EnemyDataSO FirstAuthoredBoss()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i]?.Boss != null) return Entries[i].Boss;
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
