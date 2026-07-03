using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;

namespace Rollgeon.DevConsole.Commands
{
    public sealed class TeleportCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args = { new ArgSpec("x", ArgKind.Int), new ArgSpec("y", ArgKind.Int) };
        private static readonly string[] _aliases = { "teleport" };

        public override string Name => "tp";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description => "Teletransporta al jugador al tile (x, y).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequirePlayer(ctx, out var pid, out var e2)) return e2;
            if (!RequireService<IMovementService>(ctx, out var mov, out var e3)) return e3;
            if (!TryInt(args, 0, out var x) || !TryInt(args, 1, out var y))
                return CommandResult.Fail("Usá 'tp <x> <y>'.");

            return mov.Move(pid, new GridCoord(x, y))
                ? CommandResult.Ok($"Teleport a ({x},{y}).")
                : CommandResult.Fail("No se pudo (no alcanzable / ocupado / fuera de bounds).");
        }
    }

    public sealed class FreeMoveCommand : DevCommandBase
    {
        private readonly FreeMoveController _freeMove;
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("on|off", ArgKind.Choice, optional: true, ArgProviders.OnOff)
        };

        public FreeMoveCommand(FreeMoveController freeMove) => _freeMove = freeMove;

        public override string Name => "freemove";
        public override string Description => "Movimiento libre: flechas/WASD mueven 1 tile sin turno. Toggle / on / off.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            bool on;
            if (args.Count == 0) on = _freeMove.Toggle();
            else if (string.Equals(args[0], "on", StringComparison.OrdinalIgnoreCase)) { _freeMove.Set(true); on = true; }
            else if (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase)) { _freeMove.Set(false); on = false; }
            else return CommandResult.Fail("Usá 'freemove', 'freemove on' o 'freemove off'.");

            return CommandResult.Ok($"Free move: {(on ? "ON" : "OFF")}.");
        }
    }

    public sealed class DoorCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args = { new ArgSpec("dir", ArgKind.Enum, options: ArgProviders.Doors) };

        public override string Name => "door";
        public override string Description => "Cruza la puerta en una dirección (North/South/East/West).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequireService<IDungeonService>(ctx, out var dungeon, out var e2)) return e2;
            if (args.Count < 1 || !TryEnum<DoorDirection>(args[0], out var dir))
                return CommandResult.Fail("Usá 'door <North|South|East|West>'.");

            return dungeon.EnterRoomByDoor(dir)
                ? CommandResult.Ok($"Cruzaste la puerta {dir}.")
                : CommandResult.Fail($"No se pudo cruzar {dir} (sin vecino / locked).");
        }
    }

    public sealed class FloorCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("next|<index>", ArgKind.String, optional: true, ArgProviders.Next)
        };
        private static readonly string[] _aliases = { "room", "goto" };

        public override string Name => "floor";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description =>
            "Floor warp: 'floor' lista salas, 'floor <n>' teleporta a la sala n, 'floor next' avanza de piso.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequireService<IDungeonService>(ctx, out var dungeon, out var e2)) return e2;
            var rooms = dungeon.GetAllRoomInstances();

            if (args.Count == 0)
            {
                ctx.Log.Info($"Salas del piso ({rooms.Count}):");
                int i = 0;
                foreach (var kv in rooms)
                {
                    bool current = dungeon.CurrentRoomInstance != null && dungeon.CurrentRoomInstance.InstanceId == kv.Key;
                    string label = kv.Value != null && kv.Value.Template != null ? kv.Value.Template.name : kv.Key.ToString();
                    ctx.Log.Info($"  [{i}] {label}{(current ? " (actual)" : string.Empty)}");
                    i++;
                }
                return CommandResult.Ok();
            }

            if (string.Equals(args[0], "next", StringComparison.OrdinalIgnoreCase))
            {
                var cur = dungeon.CurrentRoomInstance;
                if (cur == null) return CommandResult.Fail("No hay sala actual.");
                EventManager.Trigger(EventName.OnFloorExitRequested, cur.InstanceId);
                return CommandResult.Ok("Solicitada transición al siguiente piso.");
            }

            if (!int.TryParse(args[0], out var idx) || idx < 0 || idx >= rooms.Count)
                return CommandResult.Fail($"Usá 'floor <0..{rooms.Count - 1}>' o 'floor next'.");

            int j = 0;
            foreach (var kv in rooms)
            {
                if (j == idx)
                    return dungeon.EnterRoomByInstanceId(kv.Key)
                        ? CommandResult.Ok($"Teleport a sala [{idx}].")
                        : CommandResult.Fail("No se pudo entrar a la sala.");
                j++;
            }
            return CommandResult.Fail("Sala no encontrada.");
        }
    }

    public sealed class ClassCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args = { new ArgSpec("heroId", ArgKind.String, options: ArgProviders.Heroes) };

        public override string Name => "class";
        public override string Description => "Cambia la clase del jugador (efecto pleno al (re)iniciar la run).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IPlayerService>(ctx, out var ps, out var e1)) return e1;
            if (!RequireService<HeroCatalogSO>(ctx, out var cat, out var e2)) return e2;
            if (args.Count == 0) return CommandResult.Fail("Usá 'class <heroId>'.");

            var hero = cat.GetById(args[0]);
            if (hero == null) return CommandResult.Fail($"Clase desconocida: '{args[0]}'.");

            ps.SetPlayer(hero, ps.RunId);
            return CommandResult.Ok($"Clase seteada a {hero.DisplayName} ({hero.EntityId}). Reiniciá la run para efecto pleno.");
        }
    }
}
