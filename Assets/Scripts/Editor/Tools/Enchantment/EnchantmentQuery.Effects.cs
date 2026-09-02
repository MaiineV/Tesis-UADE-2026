using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;

namespace Rollgeon.Editor.Tools.Enchantment
{
    public static partial class EnchantmentQuery
    {
        /// <summary>
        /// Tope de profundidad para caminar árboles de <see cref="EffectData"/> anidados
        /// (una fase de <see cref="EffChain"/> puede contener otro <see cref="EffChain"/>).
        /// Mismo valor que <c>ItemQuery.MaxEffectDepth</c> / <c>BlockGraphModel.MAX_DEPTH</c>.
        /// </summary>
        public const int MaxEffectDepth = 8;

        /// <summary>
        /// Tipos concretos de <see cref="IEffect"/> que este encantamiento ejecuta, a
        /// través de todos sus <see cref="ExecuteEffectsOnDiceEvent"/>. Membresía de tipo
        /// exacto, igual que en items.
        /// </summary>
        public static IReadOnlyCollection<Type> GetEffectTypes(EnchantmentSO enchantment)
        {
            var types = new HashSet<Type>();
            if (enchantment?.Triggers == null) return types;

            var visited = new HashSet<EffectData>();
            foreach (var trigger in enchantment.Triggers)
            {
                if (trigger is not ExecuteEffectsOnDiceEvent bridge || bridge.Effects == null) continue;
                foreach (var group in bridge.Effects)
                    CollectEffectTypes(group, types, 0, visited);
            }

            return types;
        }

        /// <summary>True si el árbol de efectos implementa <typeparamref name="T"/> (tipo exacto).</summary>
        public static bool ImplementsEffect<T>(EnchantmentSO enchantment) where T : IEffect
            => GetEffectTypes(enchantment).Contains(typeof(T));

        static void CollectEffectTypes(EffectData data, HashSet<Type> into, int depth, HashSet<EffectData> visited)
        {
            if (data == null || data.Effects == null || depth >= MaxEffectDepth) return;
            // Odin puede aliasar la misma instancia de EffectData más de una vez en el grafo.
            if (!visited.Add(data)) return;

            foreach (var eff in data.Effects)
            {
                if (eff == null) continue;
                into.Add(eff.GetType());

                if (eff is EffChain chain && chain.Phases != null)
                    foreach (var phase in chain.Phases)
                        CollectEffectTypes(phase?.Effects, into, depth + 1, visited);
            }
        }
    }
}
