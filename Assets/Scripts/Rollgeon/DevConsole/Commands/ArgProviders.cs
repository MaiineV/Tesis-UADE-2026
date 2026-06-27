using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Dice;
using Rollgeon.Dungeon.Components;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>Providers de autocompletado reutilizables por los comandos.</summary>
    public static class ArgProviders
    {
        public static readonly IArgProvider OnOff = new StaticArgProvider("on", "off");
        public static readonly IArgProvider Full = new StaticArgProvider("full");
        public static readonly IArgProvider Inf = new StaticArgProvider("inf");
        public static readonly IArgProvider Next = new StaticArgProvider("next");
        public static readonly IArgProvider EnchantSub = new StaticArgProvider("add", "remove", "list");
        public static readonly IArgProvider DiceTypes = new StaticArgProvider(Enum.GetNames(typeof(DiceType)));
        public static readonly IArgProvider Doors = new StaticArgProvider(Enum.GetNames(typeof(DoorDirection)));
        public static readonly IArgProvider Stats = new StaticArgProvider(StatAccessor.SettableNames);

        public static readonly IArgProvider Items = new FuncArgProvider(ctx =>
            ctx.TryResolve<ItemCatalogSO>(out var cat) && cat != null ? cat.AllIds : Enumerable.Empty<string>());

        public static readonly IArgProvider Enchants = new FuncArgProvider(ctx =>
            ctx.TryResolve<EnchantmentCatalogSO>(out var cat) && cat != null ? cat.AllIds : Enumerable.Empty<string>());

        public static readonly IArgProvider Heroes = new FuncArgProvider(ctx =>
            ctx.TryResolve<HeroCatalogSO>(out var cat) && cat != null ? cat.AllIds : Enumerable.Empty<string>());

        public static readonly IArgProvider BagIndices = new FuncArgProvider(ctx =>
        {
            if (ctx.TryResolve<IDiceEnchantmentService>(out var svc) && svc != null && svc.IsReady && svc.Bag != null)
                return Enumerable.Range(0, svc.Bag.Dice.Count).Select(i => i.ToString());
            return Enumerable.Range(0, 5).Select(i => i.ToString());
        });
    }
}
