using System.Collections.Generic;
using System.Linq;
using Rollgeon.Attributes;
using Rollgeon.Upgrades.Dice;
using Rollgeon.Upgrades.Dice.Triggers;

namespace Rollgeon.Editor.Tools.Enchantment
{
    public static partial class EnchantmentQuery
    {
        /// <summary>Sentinela de "cualquier combo" en <see cref="EnchantmentMetrics.ComboIds"/>.</summary>
        public const string AnyComboSentinel = "*";

        /// <summary>
        /// La fila de un encantamiento en la tab de métricas. El eje económico es el
        /// <b>peso del pool</b> (+ piso mínimo), no un precio: el costo del altar es
        /// global en <c>EnchantmentConfigSO</c>.
        /// </summary>
        public sealed class EnchantmentMetrics
        {
            public EnchantmentSO Asset;
            public EnchantmentCategory Category;
            public bool IsCursed;

            /// <summary>Peso autorado en el pool; 0 con <see cref="InPool"/> = deshabilitado.</summary>
            public float Weight;
            public bool InPool;
            public int MinFloorDepth;

            public bool HasFaceFilter;
            public IReadOnlyList<EnchantmentHookEvent> TriggerEvents;

            /// <summary>Ids de combo que gatean los triggers; <see cref="AnyComboSentinel"/> = cualquier combo.</summary>
            public IReadOnlyList<string> ComboIds;

            /// <summary>Capabilities marcadas <c>[NotYetWired]</c> — configuran pero no hacen nada in-game.</summary>
            public int UnwiredCapabilities;
        }

        /// <summary>Métricas de todo el proyecto, escaneando disco.</summary>
        public static IReadOnlyList<EnchantmentMetrics> GetMetrics(EnchantmentPoolSO pool = null)
            => GetMetrics(GetAll(), pool);

        /// <summary>Forma pura de <see cref="GetMetrics(EnchantmentPoolSO)"/>.</summary>
        public static IReadOnlyList<EnchantmentMetrics> GetMetrics(
            IEnumerable<EnchantmentSO> enchantments, EnchantmentPoolSO pool = null)
        {
            pool = pool != null ? pool : EnchantmentPoolBridge.LoadDefaultPool();

            var result = new List<EnchantmentMetrics>();
            foreach (var ench in (enchantments ?? Enumerable.Empty<EnchantmentSO>()).Where(e => e != null))
                result.Add(GetMetrics(ench, pool));
            return result;
        }

        /// <summary>Métricas de un solo encantamiento.</summary>
        public static EnchantmentMetrics GetMetrics(EnchantmentSO ench, EnchantmentPoolSO pool = null)
        {
            pool = pool != null ? pool : EnchantmentPoolBridge.LoadDefaultPool();

            bool inPool = pool != null && EnchantmentPoolBridge.IsInPool(pool, ench);
            float weight = 0f;
            int minFloorDepth = 0;
            if (inPool)
            {
                EnchantmentPoolBridge.TryGetWeight(pool, ench, out weight);
                EnchantmentPoolBridge.TryGetMinFloorDepth(pool, ench, out minFloorDepth);
            }

            var events = new List<EnchantmentHookEvent>();
            var comboIds = new List<string>();
            if (ench.Triggers != null)
            {
                foreach (var trigger in ench.Triggers)
                {
                    if (trigger is not ExecuteEffectsOnDiceEvent bridge) continue;
                    if (!events.Contains(bridge.Event)) events.Add(bridge.Event);

                    bool isComboHook = bridge.Event == EnchantmentHookEvent.ComboMatched
                                    || bridge.Event == EnchantmentHookEvent.ComboPlayed;
                    if (!isComboHook) continue;

                    if (bridge.Filter is { Mode: ComboFilterMode.ComboIds, ComboIds: not null })
                    {
                        foreach (var id in bridge.Filter.ComboIds)
                            if (!string.IsNullOrEmpty(id) && !comboIds.Contains(id)) comboIds.Add(id);
                    }
                    else if (!comboIds.Contains(AnyComboSentinel))
                    {
                        comboIds.Add(AnyComboSentinel);
                    }
                }
            }

            int unwired = 0;
            if (ench.Capabilities != null)
            {
                foreach (var capability in ench.Capabilities)
                {
                    if (capability == null) continue;
                    if (capability.GetType().GetCustomAttributes(typeof(NotYetWiredAttribute), true).Length > 0)
                        unwired++;
                }
            }

            return new EnchantmentMetrics
            {
                Asset = ench,
                Category = ench.Category,
                IsCursed = ench.IsCursed(),
                Weight = weight,
                InPool = inPool,
                MinFloorDepth = minFloorDepth,
                HasFaceFilter = ench.FaceFilter != null,
                TriggerEvents = events,
                ComboIds = comboIds,
                UnwiredCapabilities = unwired,
            };
        }
    }
}
