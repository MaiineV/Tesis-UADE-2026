using System.Collections.Generic;

namespace Rollgeon.Tutorial
{
    /// <summary>
    /// Keys de la String Table <c>UI</c> para los textos del tutorial. Los textos
    /// viven en la tabla y no en el código: <see cref="TutorialFlowController"/> los
    /// resuelve con <c>LocalizedContent.Ui(key, fallbackEspañol)</c>.
    /// <para>
    /// <see cref="All"/> es el contrato que valida el test de tablas — agregar una key
    /// acá y olvidarse de poblarla en la tabla rompe el test, no el juego en runtime.
    /// </para>
    /// </summary>
    public static class TutorialTextKeys
    {
        // Sala A — movimiento libre.
        public const string Movement = "tutorial.movement";

        // Sala B — secuencia guiada del combate 1.
        public const string EnemiesIntro = "tutorial.enemies_intro";
        public const string TurnOrderIntro = "tutorial.turn_order";
        public const string ContractIcon = "tutorial.contract_icon";
        public const string MoveTeach = "tutorial.move_teach";
        public const string MoveTiles = "tutorial.move_tiles";
        public const string MoveTooFar = "tutorial.move_too_far";
        public const string StatsHp = "tutorial.stats_hp";
        public const string StatsRolls = "tutorial.stats_rolls";
        public const string AttackTeach = "tutorial.attack_teach";
        public const string TargetTeach = "tutorial.target_teach";
        public const string ThrowTeach = "tutorial.throw_teach";
        public const string DiceTeach = "tutorial.dice_teach";
        public const string RerollTeach = "tutorial.reroll_teach";
        public const string DefenseTeach = "tutorial.defense_teach";
        public const string DefenseDice = "tutorial.defense_dice";
        public const string EndTurnTeach = "tutorial.end_turn_teach";
        public const string Combat1Free = "tutorial.combat1_free";
        public const string HealUnlocked = "tutorial.heal_unlocked";
        public const string HealDice = "tutorial.heal_dice";

        // Sala C — escape y revancha.
        public const string GoToC = "tutorial.go_to_c";
        public const string EscapeTeach = "tutorial.escape_teach";
        public const string EscapeDice = "tutorial.escape_dice";
        public const string Combat2Door = "tutorial.combat2_door";
        public const string Combat2 = "tutorial.combat2";
        public const string EscapeAftermath = "tutorial.escape_aftermath";
        public const string CameraControls = "tutorial.camera_controls";
        // BUG-068: variantes de progreso mientras el paso gatea rotación + zoom —
        // se muestran una a la vez según qué le falta practicar al jugador.
        public const string CameraNeedsRotate = "tutorial.camera_needs_rotate";
        public const string CameraNeedsZoom = "tutorial.camera_needs_zoom";
        public const string MapRooms = "tutorial.map_rooms";

        // Sala D — tienda.
        public const string ShopDoor = "tutorial.shop_door";
        public const string ShopPedestal = "tutorial.shop_pedestal";
        public const string ShopPurchased = "tutorial.shop_purchased";
        public const string BackpackIcon = "tutorial.backpack_icon";

        // Sala E — encantamientos y salida.
        public const string GoToE = "tutorial.go_to_e";
        public const string EnchantRoom = "tutorial.enchant_room";
        public const string EnchantTable = "tutorial.enchant_table";
        public const string EnchantReroll = "tutorial.enchant_reroll";
        public const string EnchantDone = "tutorial.enchant_done";
        public const string DiceBagIcon = "tutorial.dicebag_icon";
        public const string Exit = "tutorial.exit";

        // Chrome del overlay.
        public const string ContinueFooter = "tutorial.continue_footer";

        /// <summary>Todas las keys del tutorial, para validación en tests.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Movement,
            EnemiesIntro, TurnOrderIntro, ContractIcon, MoveTeach, MoveTiles, MoveTooFar,
            StatsHp, StatsRolls,
            AttackTeach, TargetTeach, ThrowTeach, DiceTeach, RerollTeach, DefenseTeach,
            DefenseDice, EndTurnTeach, Combat1Free, HealUnlocked, HealDice,
            GoToC, EscapeTeach, EscapeDice, Combat2Door, Combat2,
            EscapeAftermath, CameraControls, CameraNeedsRotate, CameraNeedsZoom, MapRooms,
            ShopDoor, ShopPedestal, ShopPurchased, BackpackIcon,
            GoToE, EnchantRoom, EnchantTable, EnchantReroll, EnchantDone, DiceBagIcon, Exit,
            ContinueFooter,
        };
    }
}
