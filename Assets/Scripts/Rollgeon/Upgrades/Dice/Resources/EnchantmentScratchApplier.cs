using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Economy;
using Rollgeon.Entities.Behaviors;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Aplica los efectos de recursos acumulados en el <see cref="EnchantmentScratch"/>
    /// sobre los sistemas reales tras un evento. Compartido por <c>DiceEnchantmentService</c>
    /// y <c>ComboPassiveService</c>.
    /// </summary>
    /// <remarks>
    /// Antes cada service solo aplicaba <c>BonusGold</c> — el escudo acumulado nunca
    /// llegaba al jugador. Acá los campos legacy <c>BonusGold</c>/<c>BonusShield</c> se
    /// fusionan en los acumuladores genéricos y se resuelven por un único camino:
    /// <c>(base + Σ Add) × Π Mult</c>, con <c>Set</c> pisando la base.
    /// </remarks>
    public static class EnchantmentScratchApplier
    {
        public static void Apply(EnchantmentScratch scratch, Guid playerGuid)
        {
            if (scratch == null) return;

            // Fusionar los deltas legacy en los acumuladores genéricos → un solo code path.
            if (scratch.BonusGold != 0)
            {
                scratch.Modify(ResourceTarget.Gold, ResourceOperation.Add, scratch.BonusGold);
                scratch.BonusGold = 0;
            }
            if (scratch.BonusShield != 0)
            {
                scratch.Modify(ResourceTarget.OfStat(StatType.Shield), ResourceOperation.Add, scratch.BonusShield);
                scratch.BonusShield = 0;
            }

            if (scratch.Resources.Count == 0) return;

            foreach (var kv in scratch.Resources)
            {
                if (kv.Key.Kind == ResourceKind.Gold) ApplyGold(kv.Value);
                else ApplyStat(playerGuid, kv.Key.Stat, kv.Value);
            }
        }

        private static void ApplyGold(ResourceAccumulator acc)
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null) return;

            int current = economy.CurrentGold;
            int result = acc.Resolve(current);
            if (result < 0) result = 0;

            int delta = result - current;
            if (delta > 0) economy.Add(delta);
            else if (delta < 0) economy.Spend(-delta);
        }

        private static void ApplyStat(Guid playerGuid, StatType stat, ResourceAccumulator acc)
        {
            if (playerGuid == Guid.Empty) return;
            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return;

            int current = ReadStat(attrs, playerGuid, stat);
            int result = acc.Resolve(current);
            if (result < 0) result = 0;

            WriteStat(attrs, playerGuid, stat, result);
        }

        // RMW sobre el valor base (no el modificado): leemos para componer y reescribimos.
        private static int ReadStat(AttributesManager attrs, Guid guid, StatType stat) => stat switch
        {
            StatType.Health       => attrs.GetAttributeValue<Health, int>(guid),
            StatType.Attack       => attrs.GetAttributeValue<Attack, int>(guid),
            StatType.Speed        => attrs.GetAttributeValue<Speed, int>(guid),
            StatType.Energy       => attrs.GetAttributeValue<Energy, int>(guid),
            StatType.Shield       => attrs.GetAttributeValue<Shield, int>(guid),
            StatType.HealStrength => attrs.GetAttributeValue<HealStrength, int>(guid),
            _ => 0,
        };

        private static void WriteStat(AttributesManager attrs, Guid guid, StatType stat, int value)
        {
            switch (stat)
            {
                case StatType.Health:       attrs.SetAttributeValue<Health, int>(guid, value); break;
                case StatType.Attack:       attrs.SetAttributeValue<Attack, int>(guid, value); break;
                case StatType.Speed:        attrs.SetAttributeValue<Speed, int>(guid, value); break;
                case StatType.Energy:       attrs.SetAttributeValue<Energy, int>(guid, value); break;
                case StatType.Shield:       attrs.SetAttributeValue<Shield, int>(guid, value); break;
                case StatType.HealStrength: attrs.SetAttributeValue<HealStrength, int>(guid, value); break;
            }
        }
    }
}
