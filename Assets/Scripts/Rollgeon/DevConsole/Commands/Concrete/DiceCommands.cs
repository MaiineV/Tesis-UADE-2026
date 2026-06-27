using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dice;
using Rollgeon.Player;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.DevConsole.Commands
{
    public sealed class DiceCommand : DevCommandBase
    {
        public override string Name => "dice";
        public override string Description => "Lista los dados del jugador y sus encantamientos.";

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IDiceEnchantmentService>(ctx, out var svc, out var e)) return e;
            if (!svc.IsReady || svc.Bag == null)
                return CommandResult.Fail("El bag de dados no está inicializado (¿hay run activa?).");

            var bag = svc.Bag;
            ctx.Log.Info($"Dados ({bag.Dice.Count}):");
            for (int i = 0; i < bag.Dice.Count; i++)
            {
                var enchs = bag.GetEnchantments(i);
                var names = new List<string>();
                foreach (var en in enchs) if (en != null) names.Add(en.UpgradeId);
                ctx.Log.Info($"  [{i}] {bag.Dice[i]}  ench: {(names.Count > 0 ? string.Join(", ", names) : "-")}");
            }
            return CommandResult.Ok();
        }
    }

    public sealed class SetDiceCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("index", ArgKind.Int, options: ArgProviders.BagIndices),
            new ArgSpec("type", ArgKind.Enum, options: ArgProviders.DiceTypes)
        };

        public override string Name => "setdice";
        public override string Description => "Cambia el tipo del dado en un índice (resetea encantamientos del bag).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequirePlayer(ctx, out _, out var e1)) return e1;
            if (!RequireService<IPlayerService>(ctx, out var ps, out var e2)) return e2;
            if (!RequireService<IDiceEnchantmentService>(ctx, out var svc, out var e3)) return e3;
            if (ps.DiceBag == null) return CommandResult.Fail("El jugador no tiene bag.");

            if (!TryInt(args, 0, out var idx) || idx < 0 || idx >= ps.DiceBag.Dice.Count)
                return CommandResult.Fail($"Índice fuera de rango (0..{ps.DiceBag.Dice.Count - 1}).");
            if (args.Count < 2 || !TryEnum<DiceType>(args[1], out var type))
                return CommandResult.Fail("Tipo de dado inválido (D3/D4/D6/D8/D10/D12/D20).");

            ps.DiceBag.Dice[idx] = type;
            svc.InitializeFromBag(ps.DiceBag);
            return CommandResult.Ok($"Dado [{idx}] = {type}. (encantamientos del bag reiniciados)");
        }
    }

    public sealed class EnchantCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("add|remove|list", ArgKind.Choice, options: ArgProviders.EnchantSub),
            new ArgSpec("bagIndex", ArgKind.Int, options: ArgProviders.BagIndices),
            new ArgSpec("slot", ArgKind.Int, optional: true),
            new ArgSpec("enchId", ArgKind.String, optional: true, options: ArgProviders.Enchants)
        };

        private static readonly string[] _aliases = { "enchant" };

        public override string Name => "ench";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description =>
            "Encantamientos: 'ench add <bag> <slot> <id>' | 'ench remove <bag> <slot>' | 'ench list <bag>'.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IDiceEnchantmentService>(ctx, out var svc, out var e1)) return e1;
            if (!svc.IsReady || svc.Bag == null) return CommandResult.Fail("Bag no inicializado (¿run activa?).");
            if (args.Count == 0) return CommandResult.Fail("Usá 'ench add|remove|list ...'.");

            string sub = args[0].ToLowerInvariant();
            if (!TryInt(args, 1, out var bag) || bag < 0 || bag >= svc.Bag.Dice.Count)
                return CommandResult.Fail($"bagIndex fuera de rango (0..{svc.Bag.Dice.Count - 1}).");

            switch (sub)
            {
                case "list":
                {
                    var enchs = svc.Bag.GetEnchantments(bag);
                    ctx.Log.Info($"Dado [{bag}] {svc.Bag.Dice[bag]} — cupos {svc.Bag.GetEnchantmentSlotCount(bag)}:");
                    for (int s = 0; s < enchs.Count; s++)
                        ctx.Log.Info($"  slot {s}: {(enchs[s] != null ? enchs[s].UpgradeId : "-")}");
                    return CommandResult.Ok();
                }
                case "remove":
                {
                    if (!TryInt(args, 2, out var slot)) return CommandResult.Fail("Usá 'ench remove <bag> <slot>'.");
                    return svc.Remove(bag, slot)
                        ? CommandResult.Ok($"Quitado encantamiento de [{bag}] slot {slot}.")
                        : CommandResult.Fail("Slot vacío o inválido.");
                }
                case "add":
                {
                    if (!TryInt(args, 2, out var slot)) return CommandResult.Fail("Usá 'ench add <bag> <slot> <enchId>'.");
                    if (args.Count < 4) return CommandResult.Fail("Falta el enchId.");
                    if (!RequireService<EnchantmentCatalogSO>(ctx, out var cat, out var e2)) return e2;
                    var ench = cat.GetById(args[3]);
                    if (ench == null) return CommandResult.Fail($"Encantamiento desconocido: '{args[3]}'.");
                    var res = svc.Apply(bag, slot, ench);
                    return res.Success
                        ? CommandResult.Ok($"Aplicado {ench.UpgradeId} en [{bag}] slot {slot}.")
                        : CommandResult.Fail(res.ErrorMessage ?? "No se pudo aplicar.");
                }
                default:
                    return CommandResult.Fail("Subcomando inválido. Usá add|remove|list.");
            }
        }
    }

    public sealed class SetBagCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("d0", ArgKind.Enum, options: ArgProviders.DiceTypes),
            new ArgSpec("d1", ArgKind.Enum, options: ArgProviders.DiceTypes),
            new ArgSpec("d2", ArgKind.Enum, options: ArgProviders.DiceTypes),
            new ArgSpec("d3", ArgKind.Enum, options: ArgProviders.DiceTypes),
            new ArgSpec("d4", ArgKind.Enum, options: ArgProviders.DiceTypes)
        };

        public override string Name => "setbag";
        public override string Description => "Reemplaza el set entero de 5 dados (resetea encantamientos).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequirePlayer(ctx, out _, out var e1)) return e1;
            if (!RequireService<IPlayerService>(ctx, out var ps, out var e2)) return e2;
            if (!RequireService<IDiceEnchantmentService>(ctx, out var svc, out var e3)) return e3;
            if (ps.DiceBag?.Dice == null) return CommandResult.Fail("El jugador no tiene bag.");
            if (args.Count < 5) return CommandResult.Fail("Usá 'setbag <d0> <d1> <d2> <d3> <d4>' (5 tipos).");

            var types = new DiceType[5];
            for (int i = 0; i < 5; i++)
                if (!TryEnum<DiceType>(args[i], out types[i]))
                    return CommandResult.Fail($"Tipo inválido en posición {i}: '{args[i]}'.");

            ps.DiceBag.Dice.Clear();
            for (int i = 0; i < 5; i++) ps.DiceBag.Dice.Add(types[i]);
            svc.InitializeFromBag(ps.DiceBag);
            return CommandResult.Ok($"Bag = [{string.Join(", ", types)}]. (encantamientos reiniciados)");
        }
    }
}
