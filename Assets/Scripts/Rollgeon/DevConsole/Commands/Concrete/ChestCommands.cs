using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Chests;
using Rollgeon.Combat.Pipelines;
using Rollgeon.DevConsole.Core;
using Rollgeon.Items;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Cofres desde la consola (Feature#0046): <c>chest spawn [tier] [mimic]</c>
    /// fuerza un cofre en la sala actual ignorando el roll de frecuencia,
    /// <c>chest kill [player|enemy]</c> lo remata por el pipeline real (ejercita
    /// las ramas abrir/romper de verdad) y <c>chest info</c> muestra el estado.
    /// </summary>
    public sealed class ChestCommand : DevCommandBase
    {
        private static readonly string[] _subcommands = { "spawn", "kill", "info" };
        private static readonly string[] _tiers = { "common", "uncommon", "rare", "legendary", "god" };

        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("sub", ArgKind.Choice, optional: true,
                options: new StaticArgProvider(_subcommands)),
            new ArgSpec("arg", ArgKind.String, optional: true),
            new ArgSpec("mimic", ArgKind.String, optional: true),
        };

        public override string Name => "chest";
        public override string Description =>
            "Cofres: 'chest spawn [tier] [mimic]', 'chest kill [player|enemy]', 'chest info'.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireRun(ctx, out var runError)) return runError;
            if (!RequireService<IChestService>(ctx, out var chests, out var e1)) return e1;

            string sub = args.Count > 0 ? args[0].ToLowerInvariant() : "info";
            switch (sub)
            {
                case "spawn": return Spawn(args, chests);
                case "kill": return Kill(args, ctx, chests);
                case "info": return Info(chests);
                default:
                    return CommandResult.Fail("Usá 'chest spawn [tier] [mimic]', 'chest kill [player|enemy]' o 'chest info'.");
            }
        }

        private static CommandResult Spawn(IReadOnlyList<string> args, IChestService chests)
        {
            var tier = ItemRarity.Common;
            if (args.Count > 1 && !TryParseTier(args[1], out tier))
                return CommandResult.Fail($"Tier desconocido: '{args[1]}'. Opciones: {string.Join(", ", _tiers)}.");

            bool mimic = args.Count > 2 && args[2].Equals("mimic", StringComparison.OrdinalIgnoreCase);

            return chests.DebugSpawn(tier, mimic)
                ? CommandResult.Ok($"Cofre {tier}{(mimic ? " (Mimic)" : string.Empty)} spawneado.")
                : CommandResult.Fail("No se pudo spawnear (¿sin sala activa o sin tiles libres?).");
        }

        private static CommandResult Kill(IReadOnlyList<string> args, IDevConsoleContext ctx, IChestService chests)
        {
            if (!chests.TryGetActiveChest(out var chestGuid))
                return CommandResult.Fail("No hay cofre activo en la sala.");
            if (!RequireService<IDamagePipeline>(ctx, out var pipeline, out var e)) return e;

            // Default: golpe del jugador (rama "se abre"); 'enemy' simula el AoE
            // incidental (rama "se rompe" / clamp del Mimic).
            bool asEnemy = args.Count > 1 && args[1].Equals("enemy", StringComparison.OrdinalIgnoreCase);
            Guid source = asEnemy ? Guid.NewGuid() : ctx.PlayerGuid;

            pipeline.Resolve(new DamageContext
            {
                SourceId = source,
                TargetId = chestGuid,
                BaseDamage = 9999,
                Kind = asEnemy ? AttackKind.Environmental : AttackKind.BasicAttack,
            });
            return CommandResult.Ok(asEnemy
                ? "Golpe letal de enemigo aplicado al cofre."
                : "Golpe letal del jugador aplicado al cofre.");
        }

        private static CommandResult Info(IChestService chests)
        {
            var chest = chests.ActiveChest;
            if (chest == null) return CommandResult.Ok("Sin cofre en la sala actual.");

            return CommandResult.Ok(
                $"Cofre {chest.Tier} | fase {chest.Phase} | mimic {(chest.IsMimic ? "sí" : "no")} " +
                $"| tile ({chest.Coord.X},{chest.Coord.Y}) | guid {chest.Guid:N}");
        }

        // "default: return false" acá NO es el bug de switch-sin-caso-God que hay
        // en otros lados: cualquier string no reconocido (God incluido, si no
        // estuviera listado) falla explícitamente con el mensaje de arriba —
        // no hay degradación silenciosa a otra rareza. God SÍ está listado.
        private static bool TryParseTier(string raw, out ItemRarity tier)
        {
            switch (raw.ToLowerInvariant())
            {
                case "common": tier = ItemRarity.Common; return true;
                case "uncommon": tier = ItemRarity.Uncommon; return true;
                case "rare": tier = ItemRarity.Rare; return true;
                case "legendary": tier = ItemRarity.Legendary; return true;
                case "god": tier = ItemRarity.God; return true;
                default: tier = ItemRarity.Common; return false;
            }
        }
    }
}
