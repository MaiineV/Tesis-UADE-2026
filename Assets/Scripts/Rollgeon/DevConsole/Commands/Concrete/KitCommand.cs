using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Economy;
using Rollgeon.Items;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Setup de playtest en una línea: oro, un ejemplar de cada item del catálogo y
    /// un encantamiento random en cada dado de la bolsa. Compone lo que ya hacen
    /// <c>gold</c>, <c>giveitem</c> y <c>ench random</c> — el valor es no tener
    /// que tipear quince comandos antes de ir a probar un boss.
    /// </summary>
    public sealed class KitCommand : DevCommandBase
    {
        private const int DefaultGold = 500;

        private static readonly ArgSpec[] _args = { new ArgSpec("gold", ArgKind.Int, optional: true) };
        private static readonly string[] _aliases = { "loadout" };

        public override string Name => "kit";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description =>
            $"Kit de playtest: +oro (default {DefaultGold}), 1 de cada item del catálogo " +
            "y un encantamiento random por dado. 'kit [oro]'.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequireService<IEconomyService>(ctx, out var economy, out var e2)) return e2;

            int gold = DefaultGold;
            if (args.Count > 0 && (!int.TryParse(args[0], out gold) || gold < 0)) gold = DefaultGold;
            economy.Add(gold);

            var (itemsAdded, itemsTotal) = GiveOneOfEachItem(ctx);
            int enchants = ApplyRandomEnchantments(ctx);

            return CommandResult.Ok(
                $"Kit: +{gold} oro (total {economy.CurrentGold}) · items {itemsAdded}/{itemsTotal} · " +
                $"{enchants} encantamientos aplicados.");
        }

        private static (int added, int total) GiveOneOfEachItem(IDevConsoleContext ctx)
        {
            if (!ctx.TryResolve<IInventoryService>(out var inv) || inv == null) return (0, 0);
            if (!ctx.TryResolve<ItemCatalogSO>(out var cat) || cat == null) return (0, 0);

            int added = 0, total = 0;
            foreach (var id in cat.AllIds)
            {
                var item = cat.GetById(id);
                if (item == null) continue;
                total++;
                if (inv.AddItem(item)) added++;
            }
            return (added, total);
        }

        private static int ApplyRandomEnchantments(IDevConsoleContext ctx)
        {
            if (!ctx.TryResolve<IDiceEnchantmentService>(out var svc)
                || svc == null || !svc.IsReady || svc.Bag == null) return 0;
            if (!ctx.TryResolve<EnchantmentCatalogSO>(out var cat) || cat == null) return 0;

            var candidates = new List<EnchantmentSO>();
            foreach (var entry in cat.Entries) if (entry != null) candidates.Add(entry);
            if (candidates.Count == 0) return 0;

            // Con el stack sin techo no hay "cupos libres" que llenar — el kit
            // suma exactamente uno por dado en cada corrida.
            var rng = new Random();
            int applied = 0;
            for (int bag = 0; bag < svc.Bag.Dice.Count; bag++)
            {
                Shuffle(candidates, rng);
                foreach (var candidate in candidates)
                {
                    // ValidateApply ya rechaza incompatibles (mismo criterio que
                    // 'ench random').
                    if (!svc.ValidateApply(bag, candidate).Success) continue;
                    if (svc.Apply(bag, candidate).Success) { applied++; break; }
                }
            }
            return applied;
        }

        private static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
