using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.EnergyLib;
using Rollgeon.Combat.Pipelines;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dice;
using Rollgeon.Entities;
using Rollgeon.Player;

namespace Rollgeon.DevConsole.Commands
{
    public sealed class KillAllCommand : DevCommandBase
    {
        private static readonly string[] _aliases = { "win" };

        public override string Name => "killall";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description => "Mata a todos los enemigos del combate actual.";

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequirePlayer(ctx, out var pid, out var e2)) return e2;
            if (!RequireService<IEntityQueryService>(ctx, out var eq, out var e3)) return e3;
            if (!RequireService<IDamagePipeline>(ctx, out var dmg, out var e4)) return e4;

            var enemies = new List<Guid>();
            foreach (var en in eq.GetAllEnemiesOf(pid)) if (en != null) enemies.Add(en.InstanceId);

            int killed = 0;
            foreach (var id in enemies)
            {
                var r = dmg.Resolve(new DamageContext { SourceId = pid, TargetId = id, BaseDamage = 999999 });
                if (r.WasLethal) killed++;
            }
            return CommandResult.Ok($"Daño masivo a {enemies.Count} enemigos (letal en {killed}).");
        }
    }

    public sealed class SetEnemyHpCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args = { new ArgSpec("hp", ArgKind.Int) };

        public override string Name => "setenemyhp";
        public override string Description => "Setea el HP de todos los enemigos.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequirePlayer(ctx, out var pid, out var e2)) return e2;
            if (!RequireService<IEntityQueryService>(ctx, out var eq, out var e3)) return e3;
            if (!RequireService<AttributesManager>(ctx, out var am, out var e4)) return e4;
            if (!TryInt(args, 0, out var hp) || hp < 0) return CommandResult.Fail("Usá 'setenemyhp <n>' (n ≥ 0).");

            int n = 0;
            foreach (var en in eq.GetAllEnemiesOf(pid))
            {
                if (en == null) continue;
                am.SetAttributeValue<Health, int>(en.InstanceId, hp);
                n++;
            }
            return CommandResult.Ok($"HP = {hp} en {n} enemigos.");
        }
    }

    public sealed class EnergyCommand : DevCommandBase
    {
        private readonly InfiniteEnergyController _inf;
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("inf|<n>", ArgKind.Choice, optional: true, ArgProviders.Inf)
        };

        public EnergyCommand(InfiniteEnergyController inf) => _inf = inf;

        public override string Name => "energy";
        public override string Description => "Energía: 'energy <n>' setea, 'energy inf' toggle infinita, 'energy' muestra actual.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequirePlayer(ctx, out var pid, out var e2)) return e2;
            if (!RequireService<AttributesManager>(ctx, out var am, out var e3)) return e3;

            if (args.Count == 0)
            {
                int cur = am.GetAttributeValue<Energy, int>(pid);
                return CommandResult.Ok($"Energy = {cur}. (infinita: {(_inf.Enabled ? "ON" : "OFF")})");
            }
            if (string.Equals(args[0], "inf", StringComparison.OrdinalIgnoreCase))
            {
                bool on = _inf.Toggle();
                return CommandResult.Ok($"Energía infinita: {(on ? "ON" : "OFF")}.");
            }
            if (!int.TryParse(args[0], out var n) || n < 0) return CommandResult.Fail("Usá 'energy <n>' o 'energy inf'.");

            int max = (ctx.TryResolve<IEnergyService>(out var es) && es != null) ? es.GetMax(pid) : n;
            am.SetAttributeValue<Energy, int>(pid, n);
            EventManager.Trigger(EventName.OnEnergyChanged, pid, n, max);
            EventManager.Trigger(EventName.OnPlayerEnergyChanged, pid, n, max);
            return CommandResult.Ok($"Energy = {n}.");
        }
    }

    public sealed class SetDiceRollCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("v1", ArgKind.Int),
            new ArgSpec("v2", ArgKind.Int, optional: true),
            new ArgSpec("v3", ArgKind.Int, optional: true),
            new ArgSpec("v4", ArgKind.Int, optional: true),
            new ArgSpec("v5", ArgKind.Int, optional: true)
        };
        private static readonly string[] _aliases = { "rigroll" };

        public override string Name => "setdiceroll";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description => "Riggea la próxima tirada con las caras dadas (1 valor por dado).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<RiggedRollState>(ctx, out var rig, out var e1)) return e1;
            if (args.Count == 0) return CommandResult.Fail("Usá 'setdiceroll <v1> [v2..v5]'.");

            var values = new int[args.Count];
            for (int i = 0; i < args.Count; i++)
                if (!int.TryParse(args[i], out values[i]))
                    return CommandResult.Fail($"Valor inválido en posición {i + 1}: '{args[i]}'.");

            // Validación contra las caras del bag actual, si lo hay.
            if (ctx.TryResolve<IPlayerService>(out var ps) && ps?.DiceBag?.Dice != null)
            {
                var dice = ps.DiceBag.Dice;
                for (int i = 0; i < values.Length && i < dice.Count; i++)
                {
                    int max = dice[i].MaxFace();
                    if (values[i] < 1 || values[i] > max)
                        return CommandResult.Fail($"Dado [{i}] {dice[i]}: cara {values[i]} fuera de [1,{max}].");
                }
            }

            rig.SetNext(values);
            return CommandResult.Ok($"Próxima tirada rigueada: [{string.Join(", ", values)}].");
        }
    }
}
