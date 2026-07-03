using System;
using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Implementación default de <see cref="IDiceRoller"/>. TECHNICAL.md §6.3.
    /// </summary>
    /// <remarks>
    /// Usa un <see cref="System.Random"/> interno. El constructor parameterless
    /// siembra desde el reloj; el overload con <c>seed</c> existe para tests
    /// determinísticos (replicar la misma secuencia que el ejemplo del spec).
    /// </remarks>
    public sealed class DiceRoller : IDiceRoller
    {
        private readonly System.Random _rng;

        public DiceRoller()
        {
            _rng = new System.Random();
        }

        public DiceRoller(int seed)
        {
            _rng = new System.Random(seed);
        }

        /// <inheritdoc />
        public int[] RollAll(DiceBagSO bag)
        {
            if (bag == null) throw new ArgumentNullException(nameof(bag));
            if (bag.Dice == null)
            {
                Debug.LogWarning($"[DiceRoller] Bag '{bag.name}' tiene Dice == null. Devolviendo array vacío.");
                return Array.Empty<int>();
            }

            int n = bag.Dice.Count;
            var result = new int[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = RollFace(bag.Dice[i]);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ApplyRiggedOverride(bag, result);
#endif
            return result;
        }

        /// <inheritdoc />
        public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
        {
            if (bag == null) throw new ArgumentNullException(nameof(bag));
            if (bag.Dice == null) return Array.Empty<int>();

            int n = bag.Dice.Count;
            var result = new int[n];
            for (int i = 0; i < n; i++)
            {
                bool hold = keep != null && i < keep.Length && keep[i];
                if (hold && previousResult != null && i < previousResult.Length)
                {
                    result[i] = previousResult[i];
                }
                else
                {
                    result[i] = RollFace(bag.Dice[i]);
                }
            }
            return result;
        }

        // System.Random.Next(min, maxExclusive) — devolvemos [1, MaxFace] inclusive.
        private int RollFace(DiceType type) => _rng.Next(1, type.MaxFace() + 1);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // DevConsole: si hay una tirada "rigueada" encolada, sobrescribe las caras (clamp a
        // [1, MaxFace] por dado). No-op si nadie registró RiggedRollState o no hay nada encolado.
        // Stripped en builds de release por el guard.
        private static void ApplyRiggedOverride(DiceBagSO bag, int[] result)
        {
            if (!global::Patterns.ServiceLocator.TryGetService<RiggedRollState>(out var rig) || rig == null) return;
            if (!rig.TryConsumeNext(out var faces) || faces == null) return;

            for (int i = 0; i < result.Length && i < faces.Length; i++)
            {
                if (faces[i] <= 0) continue; // mantener la tirada normal de ese dado
                int max = bag.Dice[i].MaxFace();
                result[i] = faces[i] > max ? max : faces[i];
            }
        }
#endif
    }
}
