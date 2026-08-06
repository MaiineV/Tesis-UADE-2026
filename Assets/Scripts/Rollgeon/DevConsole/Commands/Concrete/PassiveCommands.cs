using System.Collections.Generic;
using Rollgeon.Combat.AntiRepeat;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>
    /// Flipea el pasivo anti-repetición (A/B global del jugador) entre <c>combo</c> y
    /// <c>dice</c> en runtime. Modelado en <see cref="DiceModeCommand"/>. Cambia el modo vivo
    /// vía <see cref="IAntiRepeatModeService"/> — no persiste ni toca el <c>AntiRepeatConfigSO</c>.
    /// </summary>
    public sealed class PassiveCommand : DevCommandBase
    {
        private static readonly ArgSpec[] _args =
        {
            new ArgSpec("dice|combo", ArgKind.Choice, optional: true),
        };

        public override string Name => "passive";
        public override string Description =>
            "Pasivo anti-repetición (A/B): combo = repetir el último combo hace 0 daño; " +
            "dice = bloquea un dado al azar cada turno. Sin args muestra el actual.";
        public override IReadOnlyList<ArgSpec> Args => _args;

        public override CommandResult Execute(IReadOnlyList<string> args, IDevConsoleContext ctx)
        {
            if (!RequireService<IAntiRepeatModeService>(ctx, out var svc, out var e)) return e;

            if (args.Count == 0)
                return CommandResult.Ok($"Pasivo anti-repetición: {Label(svc.Mode)}.");

            AntiRepeatMode mode;
            switch (args[0].ToLowerInvariant())
            {
                case "combo": mode = AntiRepeatMode.Combo; break;
                case "dice": mode = AntiRepeatMode.Dice; break;
                default:
                    return CommandResult.Fail("Modo inválido. Usá combo|dice.");
            }

            svc.SetMode(mode);
            return CommandResult.Ok($"Pasivo anti-repetición: {Label(mode)}.");
        }

        private static string Label(AntiRepeatMode mode) => mode == AntiRepeatMode.Dice ? "dice" : "combo";
    }
}
