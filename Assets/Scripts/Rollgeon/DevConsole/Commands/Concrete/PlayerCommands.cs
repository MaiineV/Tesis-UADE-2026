using System;
using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Core;
using Rollgeon.Economy;
using Rollgeon.Player;

namespace Rollgeon.DevConsole.Commands
{
    public sealed class HealCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("amount|full", ArgKind.Choice, optional: true, ArgProviders.Full)
        };

        public override string Name => "heal";
        public override string Description => "Cura al jugador. 'heal <n>' o 'heal full'.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequirePlayer(ctx, out var pid, out var e2)) return e2;
            if (!RequireService<IHealPipeline>(ctx, out var heal, out var e3)) return e3;

            int amount;
            if (args.Count == 0 || string.Equals(args[0], "full", StringComparison.OrdinalIgnoreCase))
            {
                if (!ctx.TryResolve<IPlayerService>(out var ps) || ps?.CurrentHero == null)
                    return CommandResult.Fail("No se pudo leer el HP máximo.");
                amount = ps.CurrentHero.BaseMaxHp; // el pipeline clampea al max
            }
            else if (!int.TryParse(args[0], out amount) || amount <= 0)
            {
                return CommandResult.Fail("Cantidad inválida. Usá 'heal <n>' o 'heal full'.");
            }

            var hc = heal.Resolve(new HealContext
            {
                SourceId = pid,
                TargetId = pid,
                BaseHeal = amount,
                IsPercentOfMax = false,
                SourceTag = "devconsole"
            });
            return CommandResult.Ok($"Curado +{hc.FinalHeal}{(hc.WasClamped ? " (clamp al max)" : string.Empty)}.");
        }
    }

    public sealed class GodCommand : DevCommandBase
    {
        private readonly GodModeController _god;
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("on|off", ArgKind.Choice, optional: true, ArgProviders.OnOff)
        };

        public GodCommand(GodModeController god) => _god = god;

        public override string Name => "god";
        public override string Description => "Vida infinita (toggle / on / off).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequirePlayer(ctx, out _, out var e)) return e;

            bool enabled;
            if (args.Count == 0) enabled = _god.Toggle();
            else if (string.Equals(args[0], "on", StringComparison.OrdinalIgnoreCase)) { _god.Enable(); enabled = true; }
            else if (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)) { _god.Disable(); enabled = false; }
            else return CommandResult.Fail("Usá 'god', 'god on' o 'god off'.");

            return CommandResult.Ok($"God mode: {(enabled ? "ON" : "OFF")}.");
        }
    }

    public sealed class GoldCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args = { new ArgSpec("amount", ArgKind.Int) };

        public override string Name => "gold";
        public override string Description => "Suma oro (o resta si es negativo).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IEconomyService>(ctx, out var eco, out var e)) return e;
            if (!TryInt(args, 0, out var n)) return CommandResult.Fail("Usá 'gold <cantidad>'.");

            if (n >= 0)
            {
                eco.Add(n);
                return CommandResult.Ok($"+{n} oro (total {eco.CurrentGold}).");
            }

            return eco.Spend(-n)
                ? CommandResult.Ok($"{n} oro (total {eco.CurrentGold}).")
                : CommandResult.Fail("Fondos insuficientes.");
        }
    }

    public sealed class SetHpCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args = { new ArgSpec("hp", ArgKind.Int) };

        public override string Name => "sethp";
        public override string Description => "Setea el HP actual del jugador.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequirePlayer(ctx, out var pid, out var e2)) return e2;
            if (!RequireService<AttributesManager>(ctx, out var am, out var e3)) return e3;
            if (!TryInt(args, 0, out var hp) || hp < 0) return CommandResult.Fail("Usá 'sethp <n>' (n ≥ 0).");

            am.SetAttributeValue<Health, int>(pid, hp);
            return CommandResult.Ok($"HP = {hp}.");
        }
    }

    public sealed class SetStatCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("stat", ArgKind.Choice, options: ArgProviders.Stats),
            new ArgSpec("value", ArgKind.Int)
        };

        public override string Name => "setstat";
        public override string Description => "Setea un stat (Health/Attack/Speed/Energy/Shield).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequirePlayer(ctx, out var pid, out var e2)) return e2;
            if (!RequireService<AttributesManager>(ctx, out var am, out var e3)) return e3;
            if (args.Count < 2 || !int.TryParse(args[1], out var v))
                return CommandResult.Fail("Usá 'setstat <stat> <valor>'.");

            if (!StatAccessor.TrySet(am, pid, args[0], v, out var err)) return CommandResult.Fail(err);
            return CommandResult.Ok($"{args[0]} = {v}.");
        }
    }
}
