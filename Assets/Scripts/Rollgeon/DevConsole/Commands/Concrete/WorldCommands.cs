using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Handoff;
using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using Rollgeon.Movement;
using Rollgeon.Player;
using Rollgeon.Run;

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

    public sealed class BossCommand : DevCommandBase
    {
        private const string ListKeyword = "list";

        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("list|<bossId>", ArgKind.String, optional: true, ArgProviders.Bosses)
        };

        public override string Name => "boss";
        public override string Description =>
            "Boss: 'boss' teleporta a la sala del piso, 'boss list' muestra el pool del piso " +
            "con pesos, 'boss <entityId>' fuerza ese boss y teleporta.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var e1)) return e1;
            if (!RequireService<IDungeonService>(ctx, out var dungeon, out var e2)) return e2;

            if (args.Count > 0 && string.Equals(args[0], ListKeyword, StringComparison.OrdinalIgnoreCase))
                return ListCurrentFloorPool(ctx);

            if (args.Count > 0)
            {
                var boss = FindBossById(ctx, args[0], out var knownIds);
                if (boss == null)
                {
                    return CommandResult.Fail(
                        $"Boss desconocido: '{args[0]}'. Pools alcanzables desde este piso: " +
                        $"[{string.Join(", ", knownIds)}].");
                }

                if (!RequireService<IBossSelectionOverride>(ctx, out var bossOverride, out var e3)) return e3;
                bossOverride.ForceNext(boss);
                ctx.Log.Info($"Forzado '{boss.EntityId}' para el próximo spawn de boss. " +
                             "Ojo: si la sala ya se visitó, los enemigos están persistidos y el " +
                             "override queda pendiente para la próxima sala de boss.");
            }

            return TeleportToBossRoom(dungeon);
        }

        private static CommandResult TeleportToBossRoom(IDungeonService dungeon)
        {
            foreach (var kv in dungeon.GetAllRoomInstances())
            {
                if (kv.Value?.Template != null && kv.Value.Template.Type == RoomType.Boss)
                {
                    return dungeon.EnterRoomByInstanceId(kv.Key)
                        ? CommandResult.Ok("Teleport a la sala de boss.")
                        : CommandResult.Fail("No se pudo entrar a la sala de boss.");
                }
            }
            return CommandResult.Fail("No se encontró sala de boss en el piso actual.");
        }

        private static CommandResult ListCurrentFloorPool(IDevConsoleContext ctx)
        {
            var layout = CurrentLayout(ctx);
            if (layout == null)
                return CommandResult.Fail("No hay layout de piso activo (IFloorProgressionService).");

            var pool = layout.BossPool;
            if (pool == null)
            {
                return CommandResult.Fail(
                    $"'{layout.name}' no tiene BossPool asignado — el boss lo define el prefab / " +
                    "EnemyPool de la sala (comportamiento previo).");
            }

            if (pool.Entries == null || pool.Entries.Count == 0)
                return CommandResult.Fail($"BossPool '{pool.name}' está vacío.");

            ctx.Log.Info($"BossPool de '{layout.name}' ({pool.Entries.Count} entries):");
            float total = 0f;
            foreach (var entry in pool.Entries)
            {
                if (BossPoolSO.IsActive(entry)) total += entry.Weight;
            }

            foreach (var entry in pool.Entries)
            {
                if (entry == null) continue;
                string id = entry.Boss != null ? entry.Boss.EntityId : "<sin boss>";
                bool active = BossPoolSO.IsActive(entry);
                string status = active
                    ? $"activo  w={entry.Weight:0.##} ({(total > 0f ? entry.Weight / total * 100f : 0f):0.#}%)"
                    : $"OFF     w={entry.Weight:0.##}{(entry.Enabled ? string.Empty : " (Enabled=off)")}";
                ctx.Log.Info($"  {status}  {id}");
            }
            return CommandResult.Ok();
        }

        /// <summary>
        /// Busca un boss por <c>EntityId</c> (o nombre de asset) en el pool del piso actual y
        /// en los de todos los pisos alcanzables por <c>NextFloor</c> — así se puede forzar un
        /// boss de otro piso. Los pools son la única fuente: nada de AssetDatabase en runtime.
        /// Devuelve también los ids conocidos, para el mensaje de error.
        /// </summary>
        private static EnemyDataSO FindBossById(
            IDevConsoleContext ctx, string query, out List<string> knownIds)
        {
            knownIds = new List<string>();
            EnemyDataSO match = null;

            foreach (var layout in ReachableLayouts(ctx))
            {
                var pool = layout.BossPool;
                if (pool?.Entries == null) continue;

                foreach (var entry in pool.Entries)
                {
                    var boss = entry?.Boss;
                    if (boss == null) continue;

                    if (!string.IsNullOrEmpty(boss.EntityId) && !knownIds.Contains(boss.EntityId))
                        knownIds.Add(boss.EntityId);

                    if (match != null) continue;
                    if (string.Equals(boss.EntityId, query, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(boss.name, query, StringComparison.OrdinalIgnoreCase))
                    {
                        match = boss;
                    }
                }
            }
            return match;
        }

        /// <summary>
        /// Piso actual + los siguientes por la cadena <c>NextFloor</c>. El <c>HashSet</c>
        /// corta ciclos si alguien encadena un layout consigo mismo por error.
        /// </summary>
        private static List<FloorLayoutSO> ReachableLayouts(IDevConsoleContext ctx)
        {
            var result = new List<FloorLayoutSO>();
            var seen = new HashSet<FloorLayoutSO>();
            var layout = CurrentLayout(ctx);
            while (layout != null && seen.Add(layout))
            {
                result.Add(layout);
                layout = layout.NextFloor;
            }
            return result;
        }

        private static FloorLayoutSO CurrentLayout(IDevConsoleContext ctx)
            => ctx.TryResolve<IFloorProgressionService>(out var progression) && progression != null
                ? progression.CurrentLayout
                : null;

        /// <summary>Autocompletado: 'list' + los EntityId de los pools alcanzables.</summary>
        public static IEnumerable<string> SuggestArgs(IDevConsoleContext ctx)
        {
            var options = new List<string> { ListKeyword };
            // El query null no matchea nada: solo nos interesa la lista de ids conocidos.
            FindBossById(ctx, null, out var ids);
            options.AddRange(ids);
            return options;
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
