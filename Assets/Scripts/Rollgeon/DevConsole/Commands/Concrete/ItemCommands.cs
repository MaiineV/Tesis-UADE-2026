using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Items;

namespace Rollgeon.DevConsole.Commands
{
    public sealed class GiveItemCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("itemId", ArgKind.String, options: ArgProviders.Items),
            new ArgSpec("count", ArgKind.Int, optional: true)
        };

        public override string Name => "giveitem";
        public override string Description => "Agrega un item al inventario por id.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IInventoryService>(ctx, out var inv, out var e1)) return e1;
            if (!RequireService<ItemCatalogSO>(ctx, out var cat, out var e2)) return e2;
            if (args.Count == 0) return CommandResult.Fail("Usá 'giveitem <itemId> [count]'.");

            var item = cat.GetById(args[0]);
            if (item == null) return CommandResult.Fail($"Item desconocido: '{args[0]}'.");

            int count = 1;
            if (args.Count > 1 && (!int.TryParse(args[1], out count) || count < 1)) count = 1;

            int added = 0;
            for (int i = 0; i < count; i++) if (inv.AddItem(item)) added++;

            return added > 0
                ? CommandResult.Ok($"+{added}× {item.DisplayName} ({item.ItemId}).")
                : CommandResult.Fail("No se agregó (¿inventario lleno?).");
        }
    }

    public sealed class ClearItemsCommand : DevCommandBase
    {
        public override string Name => "clearitems";
        public override string Description => "Quita todos los items del inventario.";

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IInventoryService>(ctx, out var inv, out var e)) return e;

            var ids = new List<string>();
            foreach (var s in inv.PassiveItems) if (s.Item != null) ids.Add(s.Item.ItemId);
            foreach (var s in inv.ActiveItems) if (s.Item != null) ids.Add(s.Item.ItemId);

            int removed = 0;
            foreach (var id in ids) if (inv.RemoveItem(id)) removed++;

            return CommandResult.Ok($"Quitados {removed} items.");
        }
    }
}
