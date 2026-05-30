using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Decorator de <see cref="IDiceRoller"/> que respeta los face filters de los
    /// encantamientos aplicados al bag. Si el <see cref="IDiceEnchantmentService"/>
    /// no está listo (sin run / sin bag), delega al inner roller — el resultado
    /// es idéntico al del kernel sin enchantments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>RNG.</b> Usa su propio <see cref="System.Random"/> — la secuencia diverge
    /// del inner cuando hay enchantments activos (per-die pick from set). Pasá
    /// un <c>seed</c> al ctor para tests determinísticos.
    /// </para>
    /// <para>
    /// <b>Reentry safety.</b> El decorator no muta el <c>RuntimeDiceBag</c>; solo
    /// lo consulta para computar caras válidas. Es seguro re-registrarlo entre
    /// runs sin teardown.
    /// </para>
    /// </remarks>
    public sealed class EnchantedDiceRoller : IDiceRoller
    {
        private readonly IDiceRoller _inner;
        private readonly System.Random _rng;
        private bool _warnedDivergence;

        public EnchantedDiceRoller(IDiceRoller inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _rng = new System.Random();
        }

        public EnchantedDiceRoller(IDiceRoller inner, int seed)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _rng = new System.Random(seed);
        }

        public int[] RollAll(DiceBagSO bag)
        {
            if (bag == null) throw new ArgumentNullException(nameof(bag));
            if (bag.Dice == null) return Array.Empty<int>();

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var service)
                || service == null || !service.IsReady)
            {
                return _inner.RollAll(bag);
            }

            int n = bag.Dice.Count;
            var result = new int[n];
            for (int i = 0; i < n; i++)
            {
                result[i] = RollOne(bag.Dice[i], i, service);
            }
            return result;
        }

        public int[] Reroll(DiceBagSO bag, int[] previousResult, bool[] keep)
        {
            if (bag == null) throw new ArgumentNullException(nameof(bag));
            if (bag.Dice == null) return Array.Empty<int>();

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var service)
                || service == null || !service.IsReady)
            {
                return _inner.Reroll(bag, previousResult, keep);
            }

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
                    result[i] = RollOne(bag.Dice[i], i, service);
                }
            }
            return result;
        }

        private int RollOne(DiceType type, int bagIndex, IDiceEnchantmentService service)
        {
            // Guard (BUG-012): el RuntimeDiceBag del enchantment service debe coincidir,
            // slot a slot, con la bolsa que estamos tirando. Si divergen (p.ej. el runtime
            // quedó cacheado contra otra bolsa), las caras de ComputeAllowedFaces pertenecen
            // a OTRO dado y clamparían éste al rango equivocado — un D20 saldría 1-6. Ante
            // divergencia, ignoramos el enchantment y tiramos el rango real del dado.
            var runtime = service.Bag;
            if (runtime == null || bagIndex < 0 || bagIndex >= runtime.Dice.Count
                || runtime.Dice[bagIndex] != type)
            {
                WarnDivergenceOnce(bagIndex, type, runtime);
                return _rng.Next(1, type.MaxFace() + 1);
            }

            var allowed = service.ComputeAllowedFaces(bagIndex);
            return PickFromSet(allowed, type);
        }

        private void WarnDivergenceOnce(int bagIndex, DiceType type, RuntimeDiceBag runtime)
        {
            if (_warnedDivergence) return;
            _warnedDivergence = true;

            var runtimeDie = runtime != null && bagIndex >= 0 && bagIndex < runtime.Dice.Count
                ? runtime.Dice[bagIndex].ToString()
                : "n/a";
            Debug.LogWarning(
                $"[EnchantedDiceRoller] RuntimeDiceBag diverge de la bolsa tirada en slot " +
                $"{bagIndex} (runtime={runtimeDie}, roll={type}). Tiro el rango real del dado " +
                $"e ignoro encantamientos — el RuntimeDiceBag no se sincronizó con la build " +
                $"(ver BUG-012). Solo se loguea una vez por roller.");
        }

        private int PickFromSet(IReadOnlyCollection<int> faces, DiceType type)
        {
            if (faces == null || faces.Count == 0)
            {
                return _rng.Next(1, type.MaxFace() + 1);
            }
            int idx = _rng.Next(0, faces.Count);
            int i = 0;
            foreach (var f in faces)
            {
                if (i == idx) return f;
                i++;
            }
            return type.MaxFace();
        }
    }
}
