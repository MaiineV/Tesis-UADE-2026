using System;
using System.Collections.Generic;
using System.Reflection;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Readers;
using Rollgeon.Items;

namespace Rollgeon.Editor.Tools.Item
{
    public static partial class ItemQuery
    {
        /// <summary>En qué recurso pega un efecto. Lo que el diseñador quiere comparar entre rarezas.</summary>
        public enum MagnitudeKind { Damage, Gold, Heal, Shield, Other }

        /// <summary>
        /// Cuánto da un efecto, cuando se puede saber sin correr el juego.
        /// </summary>
        /// <remarks>
        /// <see cref="IsDynamic"/> no es un caso de borde: media docena de ítems calculan su valor en
        /// vivo — Egoísta lee el oro actual, otros leen un contador de combo. Ese número no existe
        /// hasta que hay una partida, así que se reporta como dinámico en vez de inventarle un cero,
        /// que ensuciaría cualquier promedio con un valor falso.
        /// </remarks>
        public readonly struct EffectMagnitude
        {
            public MagnitudeKind Kind { get; }
            public int Value { get; }
            public bool IsDynamic { get; }
            public Type EffectType { get; }

            internal EffectMagnitude(MagnitudeKind kind, int value, bool isDynamic, Type effectType)
            {
                Kind = kind;
                Value = value;
                IsDynamic = isDynamic;
                EffectType = effectType;
            }
        }

        /// <summary>Las magnitudes que <paramref name="item"/> aporta, recorriendo todo su árbol de efectos.</summary>
        public static IReadOnlyList<EffectMagnitude> GetMagnitudes(ItemSO item)
        {
            var into = new List<EffectMagnitude>();
            if (item == null) return into;

            if (item.PassiveHooks != null)
                foreach (var hook in item.PassiveHooks)
                    CollectMagnitudes(hook?.Effect, into, 0, new HashSet<EffectData>());

            if (item.Type == ItemType.Active)
                CollectMagnitudes(item.OnActivate, into, 0, new HashSet<EffectData>());

            return into;
        }

        static void CollectMagnitudes(EffectData data, List<EffectMagnitude> into, int depth, HashSet<EffectData> visited)
        {
            if (data == null || data.Effects == null || depth >= MaxEffectDepth) return;
            if (!visited.Add(data)) return;

            foreach (var eff in data.Effects)
            {
                if (eff == null) continue;

                var kind = KindOf(eff.GetType());
                if (kind != MagnitudeKind.Other && TryReadAmount(eff, out int value, out bool dynamic))
                    into.Add(new EffectMagnitude(kind, value, dynamic, eff.GetType()));

                if (eff is EffChain chain && chain.Phases != null)
                    foreach (var phase in chain.Phases)
                        CollectMagnitudes(phase?.Effects, into, depth + 1, visited);
            }
        }

        /// <summary>
        /// Clasifica por nombre de tipo y no por interfaz.
        /// </summary>
        /// <remarks>
        /// Los efectos no comparten una jerarquía por recurso — <c>EffHeal</c> y <c>EffAddShield</c>
        /// no tienen nada en común salvo <c>IEffect</c>. Mapear por nombre deja que un efecto nuevo
        /// se clasifique solo con seguir la convención de nombres, en vez de obligar a tocar acá.
        /// </remarks>
        static MagnitudeKind KindOf(Type effectType)
        {
            var n = effectType.Name;
            if (n.IndexOf("Damage", StringComparison.Ordinal) >= 0
                || n.IndexOf("ComboBonus", StringComparison.Ordinal) >= 0) return MagnitudeKind.Damage;
            if (n.IndexOf("Gold", StringComparison.Ordinal) >= 0) return MagnitudeKind.Gold;
            if (n.IndexOf("Heal", StringComparison.Ordinal) >= 0) return MagnitudeKind.Heal;
            if (n.IndexOf("Shield", StringComparison.Ordinal) >= 0) return MagnitudeKind.Shield;
            return MagnitudeKind.Other;
        }

        const BindingFlags AmountFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Saca el número de un efecto, si lo tiene fijo.
        /// </summary>
        /// <remarks>
        /// Por reflexión y no con un <c>switch</c> por tipo, porque los ~19 efectos no comparten
        /// forma: unos guardan un <see cref="EffectIntReader"/> (que a su vez puede ser constante o
        /// calculado en vivo) y otros un <c>int</c> plano. Un switch quedaría desactualizado con el
        /// primer efecto nuevo; esto lo absorbe si respeta cualquiera de las dos formas.
        /// </remarks>
        static bool TryReadAmount(IEffect effect, out int value, out bool isDynamic)
        {
            value = 0;
            isDynamic = false;
            var type = effect.GetType();

            foreach (var f in type.GetFields(AmountFlags))
            {
                if (!typeof(EffectIntReader).IsAssignableFrom(f.FieldType)) continue;

                var reader = f.GetValue(effect) as EffectIntReader;
                if (reader == null) continue;
                if (reader is ReadConstantInt constant) { value = constant.Value; return true; }

                // Lee del estado del juego: no hay número que reportar fuera de una partida.
                isDynamic = true;
                return true;
            }

            foreach (var f in type.GetFields(AmountFlags))
            {
                if (f.FieldType != typeof(int)) continue;
                if (f.Name.IndexOf("amount", StringComparison.OrdinalIgnoreCase) < 0) continue;
                value = (int)f.GetValue(effect);
                return true;
            }

            return false;
        }

        /// <summary>Magnitudes fijas de una rareza, agregadas. <see cref="Dynamic"/> cuenta las que no tienen número.</summary>
        public sealed class MagnitudeSummary
        {
            public MagnitudeKind Kind { get; }
            public int Count { get; }
            public int Dynamic { get; }
            public int Min { get; }
            public int Max { get; }
            public float Average { get; }

            internal MagnitudeSummary(MagnitudeKind kind, int count, int dynamic, int min, int max, float average)
            {
                Kind = kind;
                Count = count;
                Dynamic = dynamic;
                Min = min;
                Max = max;
                Average = average;
            }
        }

        /// <summary>
        /// Daño, oro, curación y escudo agregados por rareza (spec §6.6).
        /// </summary>
        /// <remarks>
        /// Es la pregunta que el precio solo no responde: dos ítems del mismo tier pueden costar lo
        /// mismo y dar el doble uno que el otro. Las dinámicas se cuentan aparte y nunca entran al
        /// promedio — meter un cero por ellas diría que un ítem no da nada cuando puede dar mucho.
        /// </remarks>
        public static IReadOnlyDictionary<ItemRarity, IReadOnlyList<MagnitudeSummary>> GetMagnitudesByRarity(
            IEnumerable<ItemSO> items = null)
        {
            var buckets = new Dictionary<ItemRarity, Dictionary<MagnitudeKind, List<EffectMagnitude>>>();

            foreach (var item in items ?? GetAllItems())
            {
                if (item == null) continue;
                if (!buckets.TryGetValue(item.Rarity, out var byKind))
                    buckets[item.Rarity] = byKind = new Dictionary<MagnitudeKind, List<EffectMagnitude>>();

                foreach (var m in GetMagnitudes(item))
                {
                    if (!byKind.TryGetValue(m.Kind, out var list))
                        byKind[m.Kind] = list = new List<EffectMagnitude>();
                    list.Add(m);
                }
            }

            var result = new Dictionary<ItemRarity, IReadOnlyList<MagnitudeSummary>>();
            foreach (var kv in buckets)
            {
                var summaries = new List<MagnitudeSummary>();
                foreach (var byKind in kv.Value)
                {
                    int count = 0, dynamic = 0, min = int.MaxValue, max = int.MinValue;
                    long sum = 0;
                    foreach (var m in byKind.Value)
                    {
                        if (m.IsDynamic) { dynamic++; continue; }
                        count++;
                        sum += m.Value;
                        if (m.Value < min) min = m.Value;
                        if (m.Value > max) max = m.Value;
                    }
                    if (count == 0) { min = 0; max = 0; }
                    summaries.Add(new MagnitudeSummary(
                        byKind.Key, count, dynamic, min, max, count == 0 ? 0f : (float)sum / count));
                }
                summaries.Sort((a, b) => a.Kind.CompareTo(b.Kind));
                result[kv.Key] = summaries;
            }
            return result;
        }
    }
}
