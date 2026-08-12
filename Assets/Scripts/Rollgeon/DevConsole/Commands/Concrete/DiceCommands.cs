using System;
using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
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
            new ArgSpec("add|remove|list|random|roll", ArgKind.Choice, options: ArgProviders.EnchantSub),
            new ArgSpec("bagIndex", ArgKind.Int, optional: true, options: ArgProviders.BagIndices),
            new ArgSpec("slot", ArgKind.Int, optional: true),
            new ArgSpec("enchId", ArgKind.String, optional: true, options: ArgProviders.Enchants)
        };

        private static readonly string[] _aliases = { "enchant" };

        public override string Name => "ench";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description =>
            "Encantamientos: 'ench add <bag> <slot> <id>' | 'ench remove <bag> <slot>' | " +
            "'ench list <bag>' | 'ench random [bag] [slot]' (gratis, del catálogo) | " +
            "'ench roll [bag] [slot]' (altar: rolea del pool y cobra oro).";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IDiceEnchantmentService>(ctx, out var svc, out var e1)) return e1;
            if (!svc.IsReady || svc.Bag == null) return CommandResult.Fail("Bag no inicializado (¿run activa?).");
            if (args.Count == 0) return CommandResult.Fail("Usá 'ench add|remove|list|random|roll ...'.");

            string sub = args[0].ToLowerInvariant();

            // random y roll aceptan dado y slot opcionales, así que resuelven sus propios
            // argumentos; add/remove/list siguen exigiendo el bagIndex en args[1].
            if (sub == "random") return ExecuteRandom(args, ctx, svc);
            if (sub == "roll") return ExecuteRoll(args, ctx, svc);

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
                    return CommandResult.Fail("Subcomando inválido. Usá add|remove|list|random|roll.");
            }
        }

        /// <summary>
        /// Encantamiento al azar, gratis y sin pool: recorre el catálogo mezclado y aplica
        /// el primero que <c>ValidateApply</c> acepte. Es el atajo para armarse una build
        /// en dos segundos; <see cref="ExecuteRoll"/> es el que ejercita el altar real.
        /// </summary>
        private CommandResult ExecuteRandom(IReadOnlyList<string> args, IDevConsoleContext ctx,
                                            IDiceEnchantmentService svc)
        {
            if (!RequireService<EnchantmentCatalogSO>(ctx, out var cat, out var e)) return e;
            if (!TryResolveTarget(args, svc, out int bag, out int slot, out var error)) return error;

            // Sin filtrar a mano: ValidateApply ya rechaza los incompatibles (intersección
            // de caras vacía) y los redundantes con lo que el dado tiene puesto.
            var candidates = new List<EnchantmentSO>();
            foreach (var entry in cat.Entries) if (entry != null) candidates.Add(entry);
            if (candidates.Count == 0) return CommandResult.Fail("El catálogo de encantamientos está vacío.");
            Shuffle(candidates);

            foreach (var candidate in candidates)
            {
                if (!svc.ValidateApply(bag, slot, candidate).Success) continue;

                var applied = svc.Apply(bag, slot, candidate);
                if (applied.Success)
                    return CommandResult.Ok($"Aplicado {candidate.UpgradeId} en [{bag}] slot {slot}.");
            }

            return CommandResult.Fail(
                $"Ninguno de los {candidates.Count} encantamientos del catálogo es compatible con " +
                $"[{bag}] {svc.Bag.Dice[bag]} slot {slot}.");
        }

        /// <summary>
        /// El flujo del altar sin caminar hasta la sala: rolea del pool con el peso por
        /// piso y cobra el oro escalado por re-roll.
        /// </summary>
        /// <remarks>
        /// Le pasamos la sala actual como <c>roomInstanceId</c>. Si no es una sala de
        /// encantamiento, el service solo se saltea el contador de usos persistido
        /// (<c>EnchantmentRoomService.IncrementUsageState</c> early-retornea) — el roll y
        /// el cobro pasan igual.
        /// </remarks>
        private CommandResult ExecuteRoll(IReadOnlyList<string> args, IDevConsoleContext ctx,
                                          IDiceEnchantmentService svc)
        {
            if (!RequireService<IEnchantmentRoomService>(ctx, out var room, out var e)) return e;
            if (!TryResolveTarget(args, svc, out int bag, out int slot, out var error)) return error;

            var roomId = Guid.Empty;
            if (ctx.TryResolve<IDungeonService>(out var dungeon) && dungeon?.CurrentRoomInstance != null)
                roomId = dungeon.CurrentRoomInstance.InstanceId;

            var result = room.PerformEnchantment(roomId, bag, slot);
            if (!result.Success) return CommandResult.Fail(result.ErrorMessage);

            string id = result.RolledEnchantment != null ? result.RolledEnchantment.UpgradeId : "?";
            return CommandResult.Ok($"Roleado {id} en [{bag}] slot {slot} por {result.GoldPaid}G.");
        }

        /// <summary>
        /// Resuelve dado y slot de <c>args[1]</c>/<c>args[2]</c>, o los elige al azar
        /// prefiriendo un slot libre. Si el dado no tiene cupos, falla con el motivo.
        /// </summary>
        private static bool TryResolveTarget(IReadOnlyList<string> args, IDiceEnchantmentService svc,
                                            out int bag, out int slot, out CommandResult error)
        {
            slot = -1;
            error = default;
            int diceCount = svc.Bag.Dice.Count;

            if (args.Count > 1)
            {
                if (!TryInt(args, 1, out bag) || bag < 0 || bag >= diceCount)
                {
                    error = CommandResult.Fail($"bagIndex fuera de rango (0..{diceCount - 1}).");
                    return false;
                }
            }
            else
            {
                bag = PickDice(svc, diceCount);
            }

            int slotCount = svc.Bag.GetEnchantmentSlotCount(bag);
            if (slotCount <= 0)
            {
                error = CommandResult.Fail($"El dado [{bag}] {svc.Bag.Dice[bag]} no tiene cupos de encantamiento.");
                return false;
            }

            if (args.Count > 2)
            {
                if (!TryInt(args, 2, out slot) || slot < 0 || slot >= slotCount)
                {
                    error = CommandResult.Fail($"slot fuera de rango (0..{slotCount - 1}) para el dado [{bag}].");
                    return false;
                }
            }
            else
            {
                slot = PickSlot(svc, bag, slotCount);
            }

            return true;
        }

        /// <summary>Primer dado con cupo libre (en orden al azar); si están todos llenos,
        /// uno cualquiera — sobreescribir es válido, el altar hace lo mismo.</summary>
        private static int PickDice(IDiceEnchantmentService svc, int diceCount)
        {
            var order = new List<int>(diceCount);
            for (int i = 0; i < diceCount; i++) order.Add(i);
            Shuffle(order);

            foreach (int i in order)
            {
                int slotCount = svc.Bag.GetEnchantmentSlotCount(i);
                if (slotCount <= 0) continue;
                if (HasFreeSlot(svc, i, slotCount)) return i;
            }
            return order.Count > 0 ? order[0] : 0;
        }

        private static int PickSlot(IDiceEnchantmentService svc, int bag, int slotCount)
        {
            var enchs = svc.Bag.GetEnchantments(bag);
            for (int s = 0; s < slotCount; s++)
                if (enchs == null || s >= enchs.Count || enchs[s] == null) return s;

            return UnityEngine.Random.Range(0, slotCount);
        }

        private static bool HasFreeSlot(IDiceEnchantmentService svc, int bag, int slotCount)
        {
            var enchs = svc.Bag.GetEnchantments(bag);
            if (enchs == null) return true;
            for (int s = 0; s < slotCount; s++)
                if (s >= enchs.Count || enchs[s] == null) return true;
            return false;
        }

        /// <summary>Fisher-Yates con el RNG de Unity — los tests fijan el seed con
        /// <c>Random.InitState</c>.</summary>
        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    public sealed class DiceModeCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("classic|2d|3d", ArgKind.Choice, optional: true, options: ArgProviders.DiceModes),
        };

        private static readonly string[] _aliases = { "dice.mode" };

        public override string Name => "dicemode";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description =>
            "Modo de tirada (CNF-008): classic = botón Roll, 2d = arrojables canvas, 3d = física real. Sin args muestra el actual.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<Rollgeon.Dice.Throw.IDiceThrowService>(ctx, out var svc, out var e1)) return e1;

            if (args.Count == 0)
                return CommandResult.Ok($"Modo actual: {ModeLabel(svc.Mode)}.");

            Rollgeon.Dice.Throw.DiceThrowMode mode;
            switch (args[0].ToLowerInvariant())
            {
                case "classic": mode = Rollgeon.Dice.Throw.DiceThrowMode.Classic; break;
                case "2d": mode = Rollgeon.Dice.Throw.DiceThrowMode.TwoD; break;
                case "3d": mode = Rollgeon.Dice.Throw.DiceThrowMode.ThreeD; break;
                default:
                    return CommandResult.Fail("Modo inválido. Usá classic|2d|3d.");
            }

            return svc.SetMode(mode)
                ? CommandResult.Ok($"Modo de tirada: {ModeLabel(mode)}.")
                : CommandResult.Fail("Hay dados en el aire — terminá el throw y reintentá.");
        }

        private static string ModeLabel(Rollgeon.Dice.Throw.DiceThrowMode mode) => mode switch
        {
            Rollgeon.Dice.Throw.DiceThrowMode.TwoD => "2d",
            Rollgeon.Dice.Throw.DiceThrowMode.ThreeD => "3d",
            _ => "classic",
        };
    }

    public sealed class DiceMotionCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("on|off", ArgKind.Choice, optional: true),
        };

        public override string Name => "dicemotion";
        public override string Description =>
            "Animaciones del panel de dados Classic: on = animado, off = reduced motion " +
            "(instantáneo, accesibilidad). Sin args muestra el estado.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (args.Count == 0)
                return CommandResult.Ok(Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion
                    ? "Animaciones de dados: OFF (reduced motion)."
                    : "Animaciones de dados: ON.");

            switch (args[0].ToLowerInvariant())
            {
                case "on":
                    Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion = false;
                    return CommandResult.Ok("Animaciones de dados: ON.");
                case "off":
                    Rollgeon.UI.HUD.DiceAnim.DiceUiMotionPrefs.ReducedMotion = true;
                    return CommandResult.Ok("Animaciones de dados: OFF (reduced motion).");
                default:
                    return CommandResult.Fail("Usá 'dicemotion on|off'.");
            }
        }
    }

    public sealed class RerollModeCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("discard|keep", ArgKind.Choice, optional: true),
        };

        public override string Name => "rerollmode";
        public override string Description =>
            "Semántica de selección del reroll: discard = los seleccionados vuelan " +
            "(default, Balatro), keep = los seleccionados se quedan (clásico). " +
            "Sin args muestra el estado.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (args.Count == 0)
                return CommandResult.Ok(Rollgeon.Dice.RerollSelectionPrefs.KeepSelected
                    ? "Modo de reroll: KEEP (los seleccionados se quedan)."
                    : "Modo de reroll: DISCARD (los seleccionados vuelan).");

            switch (args[0].ToLowerInvariant())
            {
                case "discard":
                    Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = false;
                    return CommandResult.Ok("Modo de reroll: DISCARD (los seleccionados vuelan).");
                case "keep":
                    Rollgeon.Dice.RerollSelectionPrefs.KeepSelected = true;
                    return CommandResult.Ok("Modo de reroll: KEEP (los seleccionados se quedan).");
                default:
                    return CommandResult.Fail("Usá 'rerollmode discard|keep'.");
            }
        }
    }

    public sealed class DiceJuiceLogCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("on|off", ArgKind.Choice, optional: true),
        };

        public override string Name => "dicejuicelog";
        public override string Description =>
            "Log verboso de cada momento de juice de los dados arrojables (pickup, bounce, " +
            "settle, clatter, nudge) en la consola de Unity — para verificar qué se dispara. " +
            "Sin args muestra el estado.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (args.Count == 0)
                return CommandResult.Ok(Rollgeon.UI.HUD.DiceThrowJuice.VerboseLog
                    ? "Juice log: ON."
                    : "Juice log: OFF.");

            switch (args[0].ToLowerInvariant())
            {
                case "on":
                    Rollgeon.UI.HUD.DiceThrowJuice.VerboseLog = true;
                    return CommandResult.Ok("Juice log: ON — mirá la consola de Unity ([DiceJuice]).");
                case "off":
                    Rollgeon.UI.HUD.DiceThrowJuice.VerboseLog = false;
                    return CommandResult.Ok("Juice log: OFF.");
                default:
                    return CommandResult.Fail("Usá 'dicejuicelog on|off'.");
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
