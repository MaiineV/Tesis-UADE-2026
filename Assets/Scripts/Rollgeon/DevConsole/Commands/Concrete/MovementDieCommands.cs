using System.Collections.Generic;
using Rollgeon.DevConsole.Core;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Dado de Movimiento (§6.6): caras extra y encantamientos de su carril
    /// (<see cref="EnchantmentSlotRef.MovementDieSlot"/>). Es la fuente de prueba de
    /// "sumar caras" mientras diseño define la fuente real (GDD Dice Builder, pendiente).
    /// </summary>
    public sealed class MovementDieCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("info|faces|add|remove|list", ArgKind.Choice, options: ArgProviders.MoveDieSub),
            new ArgSpec("delta|enchId|slot", ArgKind.String, optional: true, options: ArgProviders.Enchants),
        };

        private static readonly string[] _aliases = { "movedie" };

        public override string Name => "mdie";
        public override IReadOnlyList<string> Aliases => _aliases;
        public override string Description =>
            "Dado de Movimiento: 'mdie info' | 'mdie faces <±n>' (suma caras, persiste en la run) | " +
            "'mdie add <enchId>' (solo categoría Movimiento) | 'mdie remove <slot>' | 'mdie list'.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IDiceEnchantmentService>(ctx, out var svc, out var e1)) return e1;
            if (!svc.IsReady || svc.Bag == null) return CommandResult.Fail("Bag no inicializado (¿run activa?).");

            string sub = args.Count > 0 ? args[0].ToLowerInvariant() : "info";
            const int slot = EnchantmentSlotRef.MovementDieSlot;

            switch (sub)
            {
                case "info":
                {
                    var type = DiceEnchantmentService.ResolveMovementDieType();
                    ctx.Log.Info($"Dado de Movimiento: {type} +{svc.Bag.MovementExtraFaces} caras " +
                                 $"⇒ 1..{svc.MovementDieMaxFace}; caras válidas: " +
                                 $"[{string.Join(",", svc.ComputeMovementDieFaces())}]; " +
                                 $"{svc.Bag.GetEnchantmentCount(slot)} encantamientos.");
                    return CommandResult.Ok();
                }
                case "list":
                {
                    var enchs = svc.Bag.GetEnchantments(slot);
                    ctx.Log.Info($"Dado de Movimiento — {enchs.Count} slots:");
                    for (int s = 0; s < enchs.Count; s++)
                        ctx.Log.Info($"  slot {s}: {(enchs[s] != null ? enchs[s].UpgradeId : "-")}");
                    return CommandResult.Ok();
                }
                case "faces":
                {
                    if (!TryInt(args, 1, out var delta)) return CommandResult.Fail("Usá 'mdie faces <±n>'.");
                    int extra = svc.AddMovementDieFaces(delta);
                    return CommandResult.Ok($"Dado de Movimiento: +{extra} caras ⇒ 1..{svc.MovementDieMaxFace}.");
                }
                case "add":
                {
                    if (args.Count < 2) return CommandResult.Fail("Usá 'mdie add <enchId>'.");
                    if (!RequireService<EnchantmentCatalogSO>(ctx, out var cat, out var e2)) return e2;
                    var ench = cat.GetById(args[1]);
                    if (ench == null) return CommandResult.Fail($"Encantamiento desconocido: '{args[1]}'.");
                    var res = svc.Apply(slot, ench);
                    return res.Success
                        ? CommandResult.Ok($"Aplicado {ench.UpgradeId} al dado de Movimiento (slot {res.AppliedSlotIndex}).")
                        : CommandResult.Fail(res.ErrorMessage ?? "No se pudo aplicar.");
                }
                case "remove":
                {
                    if (!TryInt(args, 1, out var enchSlot)) return CommandResult.Fail("Usá 'mdie remove <slot>'.");
                    return svc.Remove(slot, enchSlot)
                        ? CommandResult.Ok($"Quitado encantamiento del dado de Movimiento, slot {enchSlot}.")
                        : CommandResult.Fail("Slot vacío o inválido.");
                }
                default:
                    return CommandResult.Fail("Subcomando inválido. Usá info|faces|add|remove|list.");
            }
        }
    }
}
