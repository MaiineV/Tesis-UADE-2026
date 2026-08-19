using Rollgeon.DevConsole.Cheats;
using Rollgeon.DevConsole.Core;

namespace Rollgeon.DevConsole.Commands
{
    /// <summary>Arma el registry completo de comandos, inyectando los controllers de cheat.</summary>
    public static class DefaultCommands
    {
        public static DevCommandRegistry CreateDefault(IDevConsoleContext ctx,
            GodModeController god, InfiniteRollsController infRolls, FreeMoveController freeMove)
        {
            var r = new DevCommandRegistry();

            // Player
            r.Register(new HealCommand());
            r.Register(new GodCommand(god));
            r.Register(new GoldCommand());
            r.Register(new SetHpCommand());
            r.Register(new SetStatCommand());

            // Items
            r.Register(new KitCommand());
            r.Register(new GiveItemCommand());
            r.Register(new ClearItemsCommand());
            r.Register(new PotionCommand());
            r.Register(new ShopCommand());
            r.Register(new ChestCommand());

            // Dados
            r.Register(new DiceCommand());
            r.Register(new SetDiceCommand());
            r.Register(new SetBagCommand());
            r.Register(new EnchantCommand());
            r.Register(new DiceModeCommand());
            r.Register(new DiceMotionCommand());
            r.Register(new RerollModeCommand());
            r.Register(new DiceJuiceLogCommand());

            // Mundo
            r.Register(new TeleportCommand());
            r.Register(new FreeMoveCommand(freeMove));
            r.Register(new DoorCommand());
            r.Register(new FloorCommand());
            r.Register(new BossCommand());
            r.Register(new ClassCommand());
            r.Register(new TutorialCommand());

            // Combate / extras
            r.Register(new KillAllCommand());
            r.Register(new SetEnemyHpCommand());
            r.Register(new RollsCommand(infRolls));
            r.Register(new SetDiceRollCommand());

            // Steam
            r.Register(new SteamCommand());

            // Telemetría (Feature#0029)
            r.Register(new AnalyticsCommand());

            // Help último — necesita el registry ya armado.
            r.Register(new HelpCommand(r));
            return r;
        }
    }
}
