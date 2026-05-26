using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dice;

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
            var allowed = service.ComputeAllowedFaces(bagIndex);
            return PickFromSet(allowed, type);
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
