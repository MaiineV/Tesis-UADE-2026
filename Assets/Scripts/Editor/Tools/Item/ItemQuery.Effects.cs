using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item
{
    public static partial class ItemQuery
    {
        /// <summary>
        /// Depth cap for walking nested <see cref="EffectData"/> trees (an <see cref="EffChain"/>
        /// phase can itself contain another <see cref="EffChain"/>). Same value as
        /// <c>PolymorphicBlockDrawer.MAX_DEPTH</c> / <c>BlockGraphModel.MAX_DEPTH</c> — this is the
        /// exact shape of tree those already guard, no reason to pick a different number here.
        /// </summary>
        public const int MaxEffectDepth = 8;

        /// <summary>
        /// Concrete <see cref="IEffect"/> types this item implements, across
        /// <c>PassiveHooks[].Effect</c> and, for <see cref="ItemType.Active"/> items,
        /// <c>OnActivate</c>. Feeds the "everything that touches gold" filter (spec §6.1) — a
        /// caller picks a concrete type (e.g. <c>typeof(EffModifyGold)</c>) and checks membership.
        /// </summary>
        public static IReadOnlyCollection<Type> GetEffectTypes(ItemSO item)
        {
            var types = new HashSet<Type>();
            if (item == null) return types;

            if (item.PassiveHooks != null)
                foreach (var hook in item.PassiveHooks)
                    CollectEffectTypes(hook?.Effect, types, 0, new HashSet<EffectData>());

            if (item.Type == ItemType.Active)
            {
                CollectEffectTypes(item.OnActivate, types, 0, new HashSet<EffectData>());

                // Feature#0085: el modelo nuevo (UsesActiveSlot) no dispara por OnActivate —
                // corre uno de los grupos por banda/estructura. Sin esto, todo item del rework
                // reportaba cero efectos aunque tuviera OnPositiveBand cargado.
                if (item.UsesActiveSlot)
                {
                    CollectEffectTypes(item.OnNegativeBand, types, 0, new HashSet<EffectData>());
                    CollectEffectTypes(item.OnMixedBand, types, 0, new HashSet<EffectData>());
                    CollectEffectTypes(item.OnPositiveBand, types, 0, new HashSet<EffectData>());
                }
            }

            return types;
        }

        /// <summary>
        /// True if <paramref name="item"/>'s effect tree implements <typeparamref name="T"/>.
        /// Exact-type membership, not an <c>is</c> check: the filter this backs (spec §6.1) is
        /// "which concrete effects fire", and subtype matching would surface abstract base types a
        /// designer never actually picks in the effect dropdown.
        /// </summary>
        public static bool ImplementsEffect<T>(ItemSO item) where T : IEffect => GetEffectTypes(item).Contains(typeof(T));

        static void CollectEffectTypes(EffectData data, HashSet<Type> into, int depth, HashSet<EffectData> visited)
        {
            foreach (var eff in EnumerateEffects(data, depth, visited))
                into.Add(eff.GetType());
        }

        /// <summary>
        /// Every concrete <see cref="IEffect"/> reachable from <paramref name="data"/>, descending
        /// into <see cref="EffChain"/> phases. Shared by the type filter and the catalog health
        /// rules so both see the same tree.
        /// </summary>
        public static IEnumerable<IEffect> EnumerateEffects(EffectData data)
            => EnumerateEffects(data, 0, new HashSet<EffectData>());

        static IEnumerable<IEffect> EnumerateEffects(EffectData data, int depth, HashSet<EffectData> visited)
        {
            if (data == null || data.Effects == null || depth >= MaxEffectDepth) yield break;
            // Guard shared EffectData references — Odin can alias the same instance more than once
            // in a serialized graph; without this a cycle would recurse until the depth cap saves us
            // anyway, but this makes the guard explicit instead of relying on MaxEffectDepth alone.
            if (!visited.Add(data)) yield break;

            foreach (var eff in data.Effects)
            {
                if (eff == null) continue;
                yield return eff;

                if (eff is EffChain chain && chain.Phases != null)
                    foreach (var phase in chain.Phases)
                        foreach (var nested in EnumerateEffects(phase?.Effects, depth + 1, visited))
                            yield return nested;
            }
        }
    }
}
