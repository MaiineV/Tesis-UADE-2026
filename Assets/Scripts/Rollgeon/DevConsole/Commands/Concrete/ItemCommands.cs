using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dungeon;
using Rollgeon.Economy;
using Rollgeon.Items;
using Rollgeon.Shop;

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

    /// <summary>
    /// Atajo a la poción de vida. Es <c>giveitem potion.healing</c> con nombre
    /// descubrible — la poción es el único item activo del catálogo y el HUD ya tiene
    /// su slot bindeado a ese id, así que se usa en casi todo playtest.
    /// </summary>
    public sealed class PotionCommand : DevCommandBase
    {
        /// <summary>Id del <c>Item_HealingPotion.asset</c>. Mismo string que el binding
        /// del <c>ActiveItemsView</c> en <c>Canvas_PlayerStatus.prefab</c>.</summary>
        private const string PotionItemId = "potion.healing";

        private static readonly ArgSpec[] _args = { new ArgSpec("count", ArgKind.Int, optional: true) };
        private static readonly string[] _aliases = { "pocion" };

        public override string Name => "potion";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description => "Da pociones de vida. 'potion' o 'potion <n>'.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IInventoryService>(ctx, out var inv, out var e1)) return e1;
            if (!RequireService<ItemCatalogSO>(ctx, out var cat, out var e2)) return e2;

            var potion = cat.GetById(PotionItemId);
            if (potion == null)
                return CommandResult.Fail($"El catálogo no tiene '{PotionItemId}'.");

            int count = 1;
            if (args.Count > 0 && (!int.TryParse(args[0], out count) || count < 1)) count = 1;

            int added = 0;
            for (int i = 0; i < count; i++) if (inv.AddItem(potion)) added++;

            // AddItem rechaza los activos cuando se llenaron los slots — decirlo, porque
            // "no se agregó" manda a buscar el bug al lado equivocado.
            if (added == 0)
                return CommandResult.Fail(
                    $"Sin lugar: los {inv.MaxActiveSlots} slots de item activo están ocupados.");
            if (added < count)
                return CommandResult.Ok(
                    $"+{added}× {potion.DisplayName} (pedidas {count} — se llenaron los " +
                    $"{inv.MaxActiveSlots} slots activos).");
            return CommandResult.Ok($"+{added}× {potion.DisplayName}.");
        }
    }

    /// <summary>
    /// Tienda del piso desde la consola. Compra por el mismo camino que el pedestal
    /// (<see cref="ShopPurchase"/>), así que ejercita el cobro de oro real en vez de
    /// regalar el item como hace <c>giveitem</c>.
    /// </summary>
    public sealed class ShopCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("buy", ArgKind.Choice, optional: true, options: ArgProviders.ShopSub),
            new ArgSpec("slot", ArgKind.Int, optional: true)
        };

        public override string Name => "shop";
        public override string Description =>
            "Tienda: 'shop' lista los slots, 'shop buy <n>' compra cobrando el oro. " +
            "(no hay restock — es no-op en el MVP)";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequireService<IDungeonService>(ctx, out var dungeon, out var e2)) return e2;
            if (!RequireService<IShopManagerService>(ctx, out var shop, out var e3)) return e3;

            if (!TryFindShopRoom(dungeon, out var roomId, out int roomIndex))
                return CommandResult.Fail("No hay sala de tienda en el piso actual.");

            // No la inicializamos desde acá: si el prefab de la room todavía no está
            // instanciado, InitializeInternal no encuentra RewardSpawnPoints, la marca
            // como inicializada con 0 slots y la tienda queda vacía el resto de la run.
            if (!shop.IsInitialized(roomId))
                return CommandResult.Fail(
                    $"La tienda no se roleó todavía — entrá a la sala con 'floor {roomIndex}' y reintentá.");

            var slots = shop.GetSlots(roomId);
            if (slots == null || slots.Count == 0)
                return CommandResult.Fail("La tienda no tiene slots (¿la room no tiene RewardSpawnPoints?).");

            if (args.Count == 0)
            {
                ctx.Log.Info($"Tienda ({slots.Count} slots):");
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    ctx.Log.Info($"  [{i}] {ResolveName(s)} — {s.Price}G{(s.Purchased ? " (comprado)" : string.Empty)}");
                }
                return CommandResult.Ok();
            }

            if (!string.Equals(args[0], "buy", StringComparison.OrdinalIgnoreCase))
                return CommandResult.Fail("Usá 'shop' o 'shop buy <n>'.");

            if (!TryInt(args, 1, out var idx) || idx < 0 || idx >= slots.Count)
                return CommandResult.Fail($"Usá 'shop buy <0..{slots.Count - 1}>'.");

            if (!RequireService<IEconomyService>(ctx, out var economy, out var e4)) return e4;
            // Requerido y no opcional a propósito: sin inventario la compra cobraría el
            // oro y el item se perdería.
            if (!RequireService<IInventoryService>(ctx, out var inventory, out var e5)) return e5;

            var slot = slots[idx];
            string name = ResolveName(slot);
            var result = ShopPurchase.Buy(economy, inventory, shop, roomId, slot);

            if (!result.Success) return CommandResult.Fail(result.Message);

            string bought = $"Comprado {name} por {result.GoldPaid}G. Oro restante: {economy.CurrentGold}.";
            return string.IsNullOrEmpty(result.Message)
                ? CommandResult.Ok(bought)
                : CommandResult.Ok(bought + " OJO: " + result.Message);
        }

        /// <summary>
        /// Prioriza la sala actual si ya estás parado en la tienda; si no, la primera del
        /// piso. <paramref name="index"/> es la posición en el listado de <c>floor</c>,
        /// para poder decirle al usuario a dónde teleportarse.
        /// </summary>
        private static bool TryFindShopRoom(IDungeonService dungeon, out Guid roomId, out int index)
        {
            roomId = Guid.Empty;
            index = -1;

            var current = dungeon.CurrentRoomInstance;
            int i = 0;
            foreach (var kv in dungeon.GetAllRoomInstances())
            {
                if (kv.Value?.Template != null && kv.Value.Template.Type == RoomType.Shop)
                {
                    if (current != null && current.InstanceId == kv.Key)
                    {
                        roomId = kv.Key;
                        index = i;
                        return true;
                    }
                    if (index < 0)
                    {
                        roomId = kv.Key;
                        index = i;
                    }
                }
                i++;
            }
            return index >= 0;
        }

        private static string ResolveName(ShopSlot slot)
        {
            if (slot?.Item == null) return "(vacío)";
            return !string.IsNullOrEmpty(slot.Item.DisplayName) ? slot.Item.DisplayName : slot.Item.EntryId;
        }
    }
}
