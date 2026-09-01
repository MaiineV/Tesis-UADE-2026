using System.Collections.Generic;
using Patterns;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dice;
using Rollgeon.Items;
using Rollgeon.Items.Active;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Equipa un item en el slot unico de item activo. Es la unica via de adquisicion
    /// mientras la tienda y los cofres sigan dando items por el camino viejo
    /// (<c>IInventoryService.AddItem</c>) — el catalogo todavia no esta migrado.
    /// </summary>
    public sealed class EquipActiveItemCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("itemId", ArgKind.String, options: ArgProviders.Items),
        };

        public override string Name => "equipactive";
        public override string Description => "Equipa un item activo en el slot unico (descarta el que haya).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEquippedActiveItemService>(ctx, out var equipped, out var e1)) return e1;
            if (!RequireService<ItemCatalogSO>(ctx, out var cat, out var e2)) return e2;
            if (args.Count == 0) return CommandResult.Fail("Usá 'equipactive <itemId>'.");

            var item = cat.GetById(args[0]);
            if (item == null) return CommandResult.Fail($"Item desconocido: '{args[0]}'.");
            if (item.Type != ItemType.Active)
                return CommandResult.Fail($"'{item.ItemId}' no es un item activo.");

            var discarded = equipped.Equip(item);

            string tail = discarded != null ? $" (descartado: {discarded.ItemId})" : string.Empty;
            return CommandResult.Ok(
                $"Equipado {item.DisplayName} — d{item.ActiveDie.MaxFace()}, familia {item.ActiveFamily}{tail}.");
        }
    }

    /// <summary>Vacia el slot de item activo.</summary>
    public sealed class UnequipActiveItemCommand : DevCommandBase
    {
        public override string Name => "unequipactive";
        public override string Description => "Vacia el slot de item activo.";

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEquippedActiveItemService>(ctx, out var equipped, out var e)) return e;

            var discarded = equipped.Clear();
            return discarded != null
                ? CommandResult.Ok($"Slot vaciado (descartado: {discarded.ItemId}).")
                : CommandResult.Ok("El slot ya estaba vacio.");
        }
    }

    /// <summary>
    /// Estado del slot: que hay equipado, su dado y familia, el reparto de bandas y por
    /// que esta bloqueado ahora mismo.
    /// </summary>
    public sealed class ActiveItemStatusCommand : DevCommandBase
    {
        public override string Name => "activeitem";
        public override string Description => "Muestra el slot de item activo y su gate actual.";

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEquippedActiveItemService>(ctx, out var equipped, out var e)) return e;

            if (!equipped.HasItem) return CommandResult.Ok("Slot vacio.");

            var item = equipped.Current;
            int faces = item.ActiveDie.MaxFace();
            var neg = ActiveItemBands.RangeOf(ActiveItemBand.Negative, faces);
            var mix = ActiveItemBands.RangeOf(ActiveItemBand.Mixed, faces);
            var pos = ActiveItemBands.RangeOf(ActiveItemBand.Positive, faces);

            string gate = "sin servicio de activacion";
            if (ServiceLocator.TryGetService<IActiveItemActivationService>(out var act) && act != null)
                gate = act.CanActivate().ToString();

            return CommandResult.Ok(
                $"{item.DisplayName} ({item.ItemId}) — d{faces}, familia {item.ActiveFamily}\n" +
                $"  negativa {neg.Min}-{neg.Max} | mixta {mix.Min}-{mix.Max} | positiva {pos.Min}-{pos.Max}\n" +
                $"  gate: {gate}");
        }
    }
}
