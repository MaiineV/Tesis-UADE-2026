using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>Arma el registry completo de comandos, inyectando los controllers de cheat.</summary>
    public static class DefaultCommands
    {
        public static DevCommandRegistry CreateDefault(IDevConsoleContext ctx,
            GodModeController god, InfiniteEnergyController infEnergy, FreeMoveController freeMove)
        {
            var r = new DevCommandRegistry();

            // Player
            r.Register(new HealCommand());
            r.Register(new GodCommand(god));
            r.Register(new GoldCommand());
            r.Register(new SetHpCommand());
            r.Register(new SetStatCommand());

            // Items
            r.Register(new GiveItemCommand());
            r.Register(new ClearItemsCommand());

            // Dados
            r.Register(new DiceCommand());
            r.Register(new SetDiceCommand());
            r.Register(new SetBagCommand());
            r.Register(new EnchantCommand());

            // Mundo
            r.Register(new TeleportCommand());
            r.Register(new FreeMoveCommand(freeMove));
            r.Register(new DoorCommand());
            r.Register(new FloorCommand());
            r.Register(new ClassCommand());

            // Combate / extras
            r.Register(new KillAllCommand());
            r.Register(new SetEnemyHpCommand());
            r.Register(new EnergyCommand(infEnergy));
            r.Register(new SetDiceRollCommand());

            // Help último — necesita el registry ya armado.
            r.Register(new HelpCommand(r));
            return r;
        }
    }
}
