using UnityEditor;
using UnityEngine;
using Rollgeon.Tutorial;
using Rollgeon.UI;
using Rollgeon.UI.HUD.Contract;
using Rollgeon.UI.HUD.DiceBag;
using Rollgeon.UI.HUD.Inventory;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Help;

namespace Rollgeon.EditorTools.Localization
{
    /// <summary>
    /// Puebla las String Table Collections <c>UI</c> y <c>Content</c> con los textos
    /// ES/EN autorados. Idempotente: re-correrlo sobrescribe los valores sin duplicar
    /// keys (ver <see cref="LocalizationSetupTools.UpsertEntry"/>).
    /// <para>
    /// Es la fuente de verdad revisable en git de lo que dicen las tablas: editar acá
    /// y volver a correr el menú, en vez de tocar las entries a mano en el editor de
    /// Localization (los <c>.asset</c> de tabla son ilegibles en un diff).
    /// </para>
    /// </summary>
    public static class LocalizationContentSeeder
    {
        private const string ContentTable = "Content";
        private const string UiTable = "UI";

        [MenuItem("Rollgeon/Localization/Seed Content + UI")]
        public static void SeedAll()
        {
            SeedTutorial();
            SeedCombatUi();
            SeedEnchantments();
            SeedUnlockHints();
            SeedMiscContent();
            SeedBuildHelp();
            SeedStatusIcons();
            SeedSpecialTiles();
            SeedContractDrawer();
            SeedPlayerIcons();
            SeedDiceBag();
            SeedInventory();
            SeedChest();
            SeedMenuChrome();
            SeedContentBaseline();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LocalizationContentSeeder] Tablas UI + Content pobladas.");
        }

        // ==================================================================
        // Tutorial (tabla UI)
        // ==================================================================

        /// <remarks>
        /// Los <c>{0}</c> los reemplaza <c>TutorialOverlay.ApplyText</c> con la tecla
        /// viva — tienen que sobrevivir la traducción. Presupuesto: ≤22 palabras por
        /// cuadro (lo valida <c>TutorialTextBudgetTests</c>); la indicación de
        /// continuar NO va en el cuerpo — la agrega el footer del overlay.
        /// </remarks>
        private static void SeedTutorial()
        {
            Ui(TutorialTextKeys.Movement,
                "Haz clic en una casilla para moverte. Camina hasta la puerta señalada para salir de la sala.",
                "Click a tile to move. Walk to the highlighted door to leave the room.");

            Ui(TutorialTextKeys.EnemiesIntro,
                "¡Combate! Este es tu enemigo. En el tutorial tú siempre actúas primero.",
                "Combat! This is your enemy. In the tutorial you always act first.");

            Ui(TutorialTextKeys.TurnOrderIntro,
                "Arriba a la derecha está el orden de turnos: quién actúa ahora y quién sigue.",
                "Top right is the turn order: who acts now and who goes next.");

            Ui(TutorialTextKeys.ContractIcon,
                "Este es tu CONTRATO: los combos de generala y su daño. Consúltalo cuando quieras.",
                "This is your CONTRACT: the generala combos and their damage. Check it any time.");

            Ui(TutorialTextKeys.MoveTeach,
                "Tu héroe pelea cuerpo a cuerpo: primero acércate. Selecciona MOVER ({0}).",
                "Your hero fights in melee: get close first. Select MOVE ({0}).");

            Ui(TutorialTextKeys.MoveTiles,
                "Las casillas iluminadas son tu alcance este turno. Haz clic en una casilla junto al enemigo.",
                "The highlighted tiles are your reach this turn. Click a tile next to the enemy.");

            Ui(TutorialTextKeys.MoveTooFar,
                "Quedaste lejos y solo puedes moverte una vez por turno. " +
                "Pulsa FINALIZAR TURNO ({0}) y deja que se acerque.",
                "You're far away and can only move once per turn. " +
                "Press END TURN ({0}) and let them approach.");

            Ui(TutorialTextKeys.StatsHp,
                "Esta es tu VIDA: si llega a cero, la run se termina. Cuídala.",
                "This is your HEALTH: if it hits zero, the run is over. Look after it.");

            Ui(TutorialTextKeys.StatsRolls,
                "Este es tu POOL DE ROLLS: cada tirada de dados consume 1. Al terminar tu turno recuperas 5 (máximo 15).",
                "This is your ROLL POOL: every dice throw spends 1. Ending your turn grants 5 back (15 max).");

            Ui(TutorialTextKeys.AttackTeach,
                "Se desbloqueó ATACAR ({0}). Selecciónalo para elegir a quién golpear.",
                "ATTACK ({0}) is unlocked. Select it to choose who to hit.");

            Ui(TutorialTextKeys.TargetTeach,
                "Primero elige el objetivo: haz clic en el enemigo iluminado en rojo.",
                "First pick the target: click the enemy highlighted in red.");

            Ui(TutorialTextKeys.ThrowTeach,
                "¡Objetivo marcado! Estos son tus dados: sujétalos con clic izquierdo " +
                "y lánzalos con un movimiento rápido del mouse.",
                "Target locked! These are your dice: hold left click to grab them " +
                "and throw with a quick flick.");

            Ui(TutorialTextKeys.DiceTeach,
                "Arma combos (par, trío, escalera). Clic bloquea un dado; re-tira el resto " +
                "por 1 Roll cada tirada. Luego CONFIRMA.",
                "Build combos (pair, trio, straight). Click locks a die; reroll the rest " +
                "for 1 Roll per throw. Then CONFIRM.");

            Ui(TutorialTextKeys.RerollTeach,
                "Puedes volver a tirar sin tope: cada tirada consume 1 Roll de tu pool.",
                "You can reroll without limit: each throw spends 1 Roll from your pool.");

            Ui(TutorialTextKeys.DefenseTeach,
                "Te sobraron Rolls: se desbloqueó DEFENSA ({0}). Tira los dados y arma " +
                "un combo — se convierte en ESCUDO.",
                "You have Rolls left: DEFENSE unlocked ({0}). Throw the dice and build " +
                "a combo — it becomes SHIELD.");

            Ui(TutorialTextKeys.DefenseDice,
                "Lanza los dados: cuanto mejor el combo, más ESCUDO. Confirma y absorbe " +
                "el próximo golpe.",
                "Throw the dice: the better the combo, the more SHIELD. Confirm and " +
                "absorb the next hit.");

            Ui(TutorialTextKeys.EndTurnTeach,
                "¡Golpe completado! Cuando no quieras hacer nada más, pulsa " +
                "FINALIZAR TURNO ({0}) para ceder el turno.",
                "Attack complete! When you're done, press " +
                "END TURN ({0}) to hand over the turn.");

            Ui(TutorialTextKeys.Combat1Free,
                "¡Así se pelea! Repite el proceso — moverte, atacar, armar combos — hasta vencer al enemigo.",
                "That's how you fight! Repeat the process — move, attack, build combos — until the enemy is down.");

            Ui(TutorialTextKeys.HealUnlocked,
                "¡Te golpearon! Se desbloqueó CURAR ({0}): úsala en tu turno cuando te falte vida.",
                "You got hit! HEAL ({0}) is unlocked: use it on your turn when you're low on health.");

            Ui(TutorialTextKeys.HealDice,
                "Curar también usa los dados: lánzalos y supera el umbral para recuperar vida. Bloquea los altos y confirma.",
                "Healing uses the dice too: throw them and beat the threshold to recover health. Lock the high ones and confirm.");

            Ui(TutorialTextKeys.GoToC,
                "¡Bien hecho! Sigue por la puerta señalada. La otra está bloqueada — la abrirás más adelante.",
                "Well done! Head through the highlighted door. The other one is locked — you'll open it later.");

            Ui(TutorialTextKeys.EscapeTeach,
                "¡Son demasiados! Se desbloqueó FORZAR PUERTA ({0}): úsala para escapar por donde viniste. Ya estás junto a la puerta.",
                "Too many! FORCE DOOR ({0}) is unlocked: escape the way you came. You're already next to the door.");

            Ui(TutorialTextKeys.EscapeDice,
                "Forzar la puerta se resuelve con los dados: lánzalos y supera el umbral para escapar del combate.",
                "Forcing the door is resolved with dice: throw them and beat the threshold to escape the fight.");

            Ui(TutorialTextKeys.EscapeAftermath,
                "Escapar no los elimina: los enemigos se quedan en la sala, recuperan algo de vida y te esperan si vuelves.",
                "Escaping doesn't remove them: the enemies stay in the room, heal a little, and wait if you come back.");

            Ui(TutorialTextKeys.CameraControls,
                "Gira la cámara con el botón derecho. Arrastra el mapa con la rueda presionada. Zoom: rueda. Pruébalo ahora.",
                "Rotate the camera with the right button. Drag the map with the wheel pressed. Zoom: wheel. Try it now.");

            Ui(TutorialTextKeys.MapRooms,
                "Aleja el zoom para ver las salas adyacentes: sus íconos te dicen cuáles son especiales (tienda, encantamiento...).",
                "Zoom out to see the adjacent rooms: their icons tell you which ones are special (shop, enchantment...).");

            Ui(TutorialTextKeys.Combat2Door,
                "La sala se desbloqueó: entra y termina lo que empezaste.",
                "The room is unlocked: go in and finish what you started.");

            Ui(TutorialTextKeys.Combat2,
                "¡Ahora sí! Con la mejora de PAR estás listo: acaba con los dos enemigos.",
                "Now you're ready! With the PAIR upgrade, take down both enemies.");

            Ui(TutorialTextKeys.ShopDoor,
                "¡Escapaste! Se abrió la puerta de la tienda. Entra: te espera algo que te dará ventaja.",
                "You escaped! The shop door is open. Head in: something useful is waiting.");

            Ui(TutorialTextKeys.ShopPedestal,
                "Esta es la tienda. Acércate al pedestal y presiona F para comprar la mejora: tu combo PAR hará +50 de daño.",
                "This is the shop. Walk to the pedestal and press F to buy the upgrade: your PAIR combo will deal +50 damage.");

            Ui(TutorialTextKeys.ShopPurchased,
                "¡Mejora comprada! Ahora estás preparado: vuelve por donde viniste y acaba con esos enemigos.",
                "Upgrade bought! Now you're ready: head back the way you came and finish those enemies.");

            Ui(TutorialTextKeys.BackpackIcon,
                "Se desbloqueó tu INVENTARIO: tu compra vive aquí, junto a todo lo que consigas en la run.",
                "Your INVENTORY is unlocked: your purchase lives here, with everything else you find in the run.");

            Ui(TutorialTextKeys.GoToE,
                "¡Excelente! Pasa a la última sala: la de encantamientos.",
                "Excellent! Move on to the last room: the enchantment room.");

            Ui(TutorialTextKeys.EnchantRoom,
                "Esta es la mesa de encantamientos: mejora un dado a cambio de oro. Acércate al altar y presiona F.",
                "This is the enchantment table: it upgrades a die for gold. Walk to the altar and press F.");

            Ui(TutorialTextKeys.EnchantTable,
                "Elige un dado y un cupo, y confirma: el encantamiento sale al azar — y puede ser malo.",
                "Pick a die and a slot, then confirm: the enchantment is random — and it can be bad.");

            Ui(TutorialTextKeys.EnchantReroll,
                "Si no te gusta el resultado, puedes reemplazarlo pagando de nuevo: cada reemplazo cuesta más oro.",
                "Don't like the result? You can replace it by paying again: each replacement costs more gold.");

            Ui(TutorialTextKeys.EnchantDone,
                "¡Dado encantado! Ya sabes todo lo que necesitas — cierra la mesa.",
                "Die enchanted! You know everything you need — close the table.");

            Ui(TutorialTextKeys.DiceBagIcon,
                "Tu dado encantado vive en la BOLSA DE DADOS: revisa ahí tus dados y encantamientos.",
                "Your enchanted die lives in the DICE BAG: check your dice and enchantments there.");

            Ui(TutorialTextKeys.Exit,
                "Cruza la puerta señalada para empezar tu aventura de verdad.",
                "Step through the highlighted door to begin your real adventure.");

            Ui(TutorialTextKeys.ContinueFooter,
                "Haz clic para continuar",
                "Click to continue");

            // El item que vende la tienda del tutorial (Item_Tutorial_Par50,
            // autorado por TutorialAssetInstaller) — resuelve por su ItemId.
            Content("item.tutorial.par50.name", "Bonus de Par", "Pair Bonus");
            Content("item.tutorial.par50.desc", "+50 de daño al armar un Par.", "+50 damage when matching a Pair.");
        }

        // ==================================================================
        // Combate — chrome seteado por código (tabla UI)
        // ==================================================================

        private static void SeedCombatUi()
        {
            // Pasivo anti-repetición (Mode Combo): la fórmula de daño muestra esto en vez del
            // número cuando el jugador repite el último combo (DamageFormulaView).
            Ui("combat.combo_repeated_zero",
                "Combo repetido: 0 daño",
                "Repeated combo: 0 damage");

            // Toast al tocar un chip de acción no usable (ActionRejectToast):
            // título + motivo concreto resuelto por PlayerActionButtonsView.
            Ui(UiTextKeys.RejectTitle,
                "Esta acción no puede ser realizada",
                "This action can't be performed");
            Ui(UiTextKeys.RejectNoRange,
                "Sin rango al objetivo.",
                "No target in range.");
            Ui(UiTextKeys.RejectNoRolls,
                "No te quedan Rolls.",
                "No Rolls left.");
            Ui(UiTextKeys.RejectUsed,
                "Ya la usaste este turno.",
                "Already used this turn.");
            Ui(UiTextKeys.RejectFullHealth,
                "Tienes la vida completa.",
                "Your health is already full.");
            Ui(UiTextKeys.RejectNoDoor,
                "No estás junto a una puerta.",
                "You're not next to a door.");
            Ui(UiTextKeys.RejectNotYourTurn,
                "No es tu turno.",
                "It's not your turn.");
            Ui(UiTextKeys.RejectNoPotion,
                "No tienes poción disponible.",
                "You have no potion available.");
        }

        // ==================================================================
        // Encantamientos (tabla Content)
        // ==================================================================

        /// <remarks>
        /// <c>gold_on_roll</c>, <c>only_evens</c> y <c>parity_gamble</c> estaban
        /// autorados en inglés en su SO, así que hasta el español salía mal. El ES de
        /// <c>gold_on_roll</c> es "Ambicioso" y no "Codicioso" porque ese nombre ya lo
        /// usa otro encantamiento distinto (<c>ench.codicioso</c>).
        /// </remarks>
        private static void SeedEnchantments()
        {
            Ench("afilado", "Afilado", "Sharpened",
                "El resultado mínimo es siempre la mitad del máximo redondeado hacia arriba. Un d6 afilado nunca saca menos de 3.",
                "The minimum result is always half the maximum, rounded up. A sharpened d6 never rolls below 3.");

            Ench("ancla", "Ancla", "Anchor",
                "Si este dado se bloquea entre tiradas, su resultado aumenta +1 por tirada bloqueada (máx +3).",
                "If this die is held between rolls, its result increases by +1 per held roll (max +3).");

            Ench("avaro", "Avaro", "Miser",
                "Otorga 3 de oro extra al completar un combo de trío o superior.",
                "Grants 3 extra gold when you complete a trio combo or better.");

            Ench("caras_centrales", "Caras Centrales", "Middle Faces",
                "Bloquea el cuarto superior e inferior. d8:{3,4,5,6} d12:{4,5,6,7,8,9}",
                "Blocks the top and bottom quarter. d8:{3,4,5,6} d12:{4,5,6,7,8,9}");

            Ench("cargado", "Cargado", "Loaded",
                "Una vez por combate puedes volver a tirar este dado y quedarte con el mayor resultado.",
                "Once per combat you can reroll this die and keep the higher result.");

            Ench("codicioso", "Codicioso", "Covetous",
                "El dado participa en cualquier combo y suma +3 de oro.",
                "The die joins any combo and grants +3 gold.");

            Ench("comodin", "Comodín", "Wild",
                "Este dado cuenta como cualquier número para propósitos de combo (escaleras, trío, generala).",
                "This die counts as any number for combo purposes (straights, trio, generala).");

            Ench("escalador", "Escalador", "Climber",
                "Para escaleras, este dado cuenta como su valor y como valor+1 simultáneamente.",
                "For straights, this die counts as both its value and its value+1 at the same time.");

            Ench("escudado", "Escudado", "Shielded",
                "Si se utiliza en un combo de full house da 2 más de escudo.",
                "If used in a full house combo it grants 2 extra shield.");

            Ench("extremos", "Extremos", "Extremes",
                "Solo muestra el cuarto superior e inferior. d8:{1,2,7,8} d12:{1,2,3,10,11,12}",
                "Only shows the top and bottom quarter. d8:{1,2,7,8} d12:{1,2,3,10,11,12}");

            Ench("fortaleza", "Fortaleza", "Fortress",
                "El dado muestra su valor máximo y participa en combo; genera 2 puntos de escudo.",
                "The die shows its maximum value and joins the combo; it generates 2 shield points.");

            Ench("fragil", "Frágil", "Fragile",
                "50% de chance de que el dado no cuente para el combo.",
                "50% chance the die doesn't count toward the combo.");

            Ench("gemelo", "Gemelo", "Twin",
                "Si este dado muestra el mismo número que otro en la tirada, ambos valen x1.5 para el combo.",
                "If this die shows the same number as another in the roll, both count x1.5 toward the combo.");

            Ench("gold_on_roll", "Ambicioso", "Greed",
                "Si el dado no participa en ningún combo, otorga 2 de oro.",
                "If the die isn't part of a combo, it grants 2 gold.");

            Ench("impar", "Impar", "Odd",
                "Solo muestra caras impares. d6:{1,3,5} d8:{1,3,5,7} d12:{1,3,5,7,9,11}",
                "Only shows odd faces. d6:{1,3,5} d8:{1,3,5,7} d12:{1,3,5,7,9,11}");

            Ench("invertido", "Invertido", "Inverted",
                "El resultado es máximo+1 menos el resultado. Un d6 que saca 1 cuenta como 6.",
                "The result becomes max+1 minus the result. A d6 that rolls 1 counts as 6.");

            Ench("lento", "Lento", "Sluggish",
                "Este dado no se puede bloquear entre tiradas.",
                "This die cannot be held between rolls.");

            Ench("mercader", "Mercader", "Merchant",
                "Otorga 5 de oro al completar una escalera.",
                "Grants 5 gold when you complete a straight.");

            Ench("mimetico", "Mimético", "Mimic",
                "Copia el resultado del último dado vuelto a tirar en esta tirada para propósitos de combo.",
                "Copies the result of the last rerolled die in this roll for combo purposes.");

            Ench("mitad_inferior", "Mitad Inferior", "Lower Half",
                "Solo muestra la mitad inferior del dado. d6:{1,2,3} d8:{1,2,3,4} d12:{1,2,3,4,5,6}",
                "Only shows the lower half of the die. d6:{1,2,3} d8:{1,2,3,4} d12:{1,2,3,4,5,6}");

            Ench("mitad_superior", "Mitad Superior", "Upper Half",
                "Solo muestra la mitad superior del dado. d6:{4,5,6} d8:{5,6,7,8} d12:{7,8,9,10,11,12}",
                "Only shows the upper half of the die. d6:{4,5,6} d8:{5,6,7,8} d12:{7,8,9,10,11,12}");

            Ench("multiplo_de_3", "Múltiplo de 3", "Multiple of 3",
                "Solo muestra múltiplos de 3. d12:{3,6,9,12}",
                "Only shows multiples of 3. d12:{3,6,9,12}");

            Ench("no_primo", "No Primo", "Non-Prime",
                "Solo muestra números no primos (incluye el 1). d8:{1,4,6,8}",
                "Only shows non-prime numbers (1 included). d8:{1,4,6,8}");

            Ench("only_evens", "Solo Pares", "Only Evens",
                "Este dado solo saca números pares.",
                "This die only rolls even numbers.");

            Ench("oxidado", "Oxidado", "Rusty",
                "El dado pierde 1 de su resultado final (mínimo 1).",
                "The die loses 1 from its final result (minimum 1).");

            Ench("par", "Par", "Even",
                "Solo muestra caras pares. d6:{2,4,6} d8:{2,4,6,8} d12:{2,4,6,8,10,12}",
                "Only shows even faces. d6:{2,4,6} d8:{2,4,6,8} d12:{2,4,6,8,10,12}");

            Ench("parity_gamble", "Apuesta de Paridad", "Parity Gamble",
                "Si el dado saca un número impar multiplica x3; si no, x0.",
                "If this die rolls an odd number it multiplies x3; otherwise x0.");

            Ench("pesado", "Pesado", "Heavy",
                "Suma +2 al resultado final del dado.",
                "Adds +2 to the die's final result.");

            Ench("primo", "Primo", "Prime",
                "Solo muestra números primos. d12:{2,3,5,7,11}",
                "Only shows prime numbers. d12:{2,3,5,7,11}");

            Ench("resonante", "Resonante", "Resonant",
                "Si 2 o más dados muestran el mismo número en la tirada final, este dado suma su valor dos veces al combo.",
                "If 2 or more dice show the same number in the final roll, this die adds its value to the combo twice.");

            Ench("sediento", "Sediento", "Thirsty",
                "Cada vez que participa en un combo consume 2 de oro. Sin oro no puede participar.",
                "Each time it joins a combo it consumes 2 gold. With no gold it can't take part.");

            Ench("torpe", "Torpe", "Clumsy",
                "Obliga a volver a tirar todos los dados en el turno 2 del combate.",
                "Forces a full reroll on turn 2 of the combat.");

            Ench("volatil", "Volátil", "Volatile",
                "Al sacar el máximo, el resultado se duplica. Al sacar el mínimo, vale 0.",
                "On a maximum roll the result doubles. On a minimum roll it's worth 0.");
        }

        // ==================================================================
        // Pistas de desbloqueables (tabla Content)
        // ==================================================================

        /// <remarks>
        /// Siembra el trío completo de cada unlock: <c>.name</c> (título), <c>.desc</c>
        /// (texto una vez desbloqueado) y <c>.hint</c> (lo único visible mientras está
        /// BLOQUEADO). Los resuelven <c>LocalizedContent.Name/Description/Hint</c>.
        /// <para>
        /// El <c>.name</c>/<c>.desc</c> de d8/d10/berserker/gambler vivía solo en las tablas
        /// (cargado a mano en el commit i18n original), NO acá — así que una regeneración de
        /// tablas desde el seeder los borraba y su descripción caía al fallback español bajo
        /// inglés (BUG-026). Ahora el seeder es la fuente de verdad COMPLETA: los valores son
        /// idénticos a los de las tablas, así que re-sembrar es idempotente.
        /// </para>
        /// </remarks>
        private static void SeedUnlockHints()
        {
            Content("unlock.dice.d8.name", "Dado D8", "D8 Die");
            Content("unlock.dice.d8.desc",
                "El D8 queda disponible en la pantalla de armado de build.",
                "The D8 becomes available on the build screen.");
            Content("unlock.dice.d8.hint",
                "Domina el dado estándar: gana una run confiando solo en el clásico de seis caras.",
                "Master the standard die: win a run relying only on the classic six-sider.");

            Content("unlock.dice.d10.name", "Dado D10", "D10 Die");
            Content("unlock.dice.d10.desc",
                "El D10 queda disponible en la pantalla de armado de build.",
                "The D10 becomes available on the build screen.");
            Content("unlock.dice.d10.hint",
                "Hay una receta exacta de dados que abre esta puerta. Experimenta con la mezcla.",
                "There's an exact dice recipe that opens this door. Experiment with the mix.");

            Content("unlock.class.berserker.name", "Berserker", "Berserker");
            Content("unlock.class.berserker.desc",
                "El Berserker queda seleccionable en la pantalla de selección de personaje.",
                "The Berserker becomes selectable on the character selection screen.");
            Content("unlock.class.berserker.hint",
                "Demuestra fuerza de ocho caras: lleva el dado nuevo a una victoria.",
                "Prove your eight-sided strength: carry the new die to a victory.");

            Content("unlock.class.gambler.name", "Gambler", "Gambler");
            Content("unlock.class.gambler.desc",
                "El Gambler queda seleccionable en la pantalla de selección de personaje.",
                "The Gambler becomes selectable on the character selection screen.");
            Content("unlock.class.gambler.hint",
                "Un verdadero apostador no deja ninguna jugada del Contrato sin cobrar.",
                "A true gambler never leaves a Contract play uncashed.");

            // Clases aún no implementadas (Mage/Rogue) — gateadas con
            // ComingSoonCondition hasta que exista su ClassHeroSO.
            Content("unlock.class.mage.name", "Mago", "Mage");
            Content("unlock.class.mage.desc",
                "El Mago quedará seleccionable en una futura versión.",
                "The Mage will become selectable in a future version.");
            Content("unlock.class.mage.hint", "Próximamente", "Coming soon");
            Content("unlock.class.rogue.name", "Pícaro", "Rogue");
            Content("unlock.class.rogue.desc",
                "El Pícaro quedará seleccionable en una futura versión.",
                "The Rogue will become selectable in a future version.");
            Content("unlock.class.rogue.hint", "Próximamente", "Coming soon");
        }

        // ==================================================================
        // Barrido del resto de Content que seguía sin traducir
        // ==================================================================

        private static void SeedMiscContent()
        {
            // Recompensas de personaje — se veían en inglés incluso en español.
            Content("char_rew.attack_plus_3.name", "Ataque +3", "Attack +3");
            Content("char_rew.energy_plus_1.name", "+1 Roll por turno", "+1 Roll per turn");
            Content("char_rew.hp_plus_5.name", "Vida máxima +5", "Max Health +5");
            Content("char_rew.speed_plus_2.name", "Velocidad +2", "Speed +2");

            // Pasivas de combo.
            Content("combo.pass.gold_on_ladder.name", "Codicia en Escalera", "Greed on Ladder");
            Content("combo.pass.gold_on_ladder.desc", "Cada combo de Escalera otorga oro.", "Each Ladder combo grants gold.");
            Content("combo_pass.pair_plus_50.name", "Bonus de Par", "Pair Bonus");
            Content("combo_pass.pair_plus_50.desc", "+50 de daño al armar un Par.", "+50 damage when matching a Pair.");

            // Nombres de sala — los muestra el nav del HUD; eran placeholders ("RoomCombat").
            Content("Start01.name", "Sala Inicial", "Starting Room");
            Content("Start02.name", "Sala Inicial", "Starting Room");
            Content("RoomCombat01.name", "Sala de Combate", "Combat Room");
            Content("RoomCombat02.name", "Sala de Combate", "Combat Room");
            Content("RoomCombat03.name", "Sala de Combate", "Combat Room");
            Content("RoomCombatFloorTwo01.name", "Sala de Combate", "Combat Room");
            Content("CombatBoss.name", "Sala del Jefe", "Boss Room");
            Content("Shop01.name", "Tienda", "Shop");
            Content("Potion01.name", "Sala de Pociones", "Potion Room");
            Content("Enchantment.name", "Sala de Encantamiento", "Enchantment Room");
            Content("room_tutorial_combat_b.name", "Tutorial — Combate 1", "Tutorial — Combat 1");
            Content("room_tutorial_combat_c.name", "Tutorial — Combate 2", "Tutorial — Combat 2");
        }

        // ==================================================================
        // Guía de armado de bolsa (tabla UI)
        // ==================================================================

        /// <remarks>
        /// Copy descriptiva, no imperativa: los pasos corren con el dim capturando el
        /// click, así que el jugador no puede ejecutar la acción mientras la lee.
        /// </remarks>
        private static void SeedBuildHelp()
        {
            Ui(BuildHelpTextKeys.Pool,
                "Estos son los dados de tu clase. Haz clic en uno para sumarlo a la bolsa; " +
                "el número de cada fila dice cuántos puedes llevar de ese tipo.",
                "These are your class dice. Click one to add it to your bag; the number on each " +
                "row shows how many of that type you can carry.");

            Ui(BuildHelpTextKeys.Strip,
                "Tu bolsa se arma aquí, siempre ordenada de menor a mayor. Haz clic en un dado " +
                "de la tira para devolverlo al pool.",
                "Your bag is built here, always sorted from lowest to highest. Click a die in the " +
                "strip to send it back to the pool.");

            Ui(BuildHelpTextKeys.Clear,
                "Limpiar vacía la bolsa entera y te deja empezar de cero.",
                "Clear empties the whole bag so you can start over.");

            Ui(BuildHelpTextKeys.Confirm,
                "Cuando completes la bolsa, Confirmar se habilita y arranca la run.",
                "Once your bag is full, Confirm unlocks and starts the run.");
        }

        // ==================================================================
        // Estados del player (pie del tooltip de la fila de estados)
        // ==================================================================

        /// <remarks>
        /// Solo el PIE del tooltip. El nombre y la descripción de cada estado salen de sus
        /// propias keys (<c>name.*</c> / <c>desc.*</c>), porque son del estado y no del
        /// sistema que los muestra.
        /// </remarks>
        private static void SeedStatusIcons()
        {
            Ui(StatusTextKeys.Active, "Activada", "Active");
            Ui(StatusTextKeys.Inactive, "Desactivada", "Inactive");

            // {0} = turnos restantes. El caso de 1 va por su propia key para no decir
            // "1 turnos" — en inglés la diferencia es igual de visible.
            Ui(StatusTextKeys.Duration, "Dura {0} turnos", "Lasts {0} turns");
            Ui(StatusTextKeys.DurationLastTurn, "Último turno", "Last turn");

            // Badge corto bajo el ícono (distinto del pie del tooltip de arriba).
            Ui(StatusTextKeys.BadgeOneTurn, "1 Turno", "1 Turn");
            Ui(StatusTextKeys.BadgeTurns, "{0} Turnos", "{0} Turns");

            // Estados con duración (Casillas Especiales). Nombre + descripción del estado,
            // que son SUYOS (los publica su provider), a diferencia del pie del sistema.
            Content("status.poison.name", "Envenenado", "Poisoned");
            Content("status.poison.desc",
                "Recibís daño al inicio de cada turno. Volver a pisar Veneno refresca la duración.",
                "You take damage at the start of each turn. Stepping on Poison again refreshes the duration.");
            Content("status.stun.name", "Aturdido", "Stunned");
            Content("status.stun.desc",
                "Perdés tu próximo turno.",
                "You lose your next turn.");
        }

        // ==================================================================
        // Casillas Especiales — tooltips de tile (GDD §16)
        // ==================================================================

        /// <summary>
        /// Re-siembra SOLO las claves de casillas especiales. Existe para autorear una
        /// casilla nueva sin correr <see cref="SeedAll"/>: el seeder completo reescribe
        /// las 3 tablas desde el código, así que si algún valor de tabla se editó a mano
        /// alguna vez, un run completo lo revierte en silencio. Esto acota el blast radius.
        /// </summary>
        [MenuItem("Rollgeon/Localization/Seed Special Tiles")]
        public static void SeedSpecialTilesOnly()
        {
            SeedSpecialTiles();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LocalizationContentSeeder] Claves de casillas especiales pobladas.");
        }

        private static void SeedSpecialTiles()
        {
            // Líneas del sistema (números duros del tooltip). {0} = valor.
            Ui("tile.tooltip.enterdamage", "Daño al entrar: {0}", "Damage on enter: {0}");
            Ui("tile.tooltip.turndamage", "Daño por turno encima: {0}", "Damage per turn standing: {0}");
            Ui("tile.tooltip.heal", "Cura al terminar el turno: {0}", "Heals at end of turn: {0}");
            Ui("tile.tooltip.duration", "Dura {0} rondas", "Lasts {0} rounds");

            // Nombre + descripción por casilla del catálogo.
            Content("tile.spikes.name", "Pinchos", "Spikes");
            Content("tile.spikes.desc",
                "Trampa armada: pincha al entrar o al ser empujado encima. Se rearma cada ronda.",
                "Armed trap: stabs when entered or pushed onto. Rearms each round.");
            Content("tile.fire.name", "Fuego", "Fire");
            Content("tile.fire.desc",
                "Quema al entrar y en cada inicio de turno parado encima. Afecta también a voladoras.",
                "Burns on enter and at each turn start while standing on it. Affects flying units too.");
            Content("tile.firetemp.name", "Fuego Temporal", "Temporary Fire");
            Content("tile.firetemp.desc",
                "Fuego que se apaga solo tras unas rondas.",
                "Fire that burns out on its own after a few rounds.");
            Content("tile.firecroupier.name", "Fuego de la Banca", "House Fire");
            Content("tile.firecroupier.desc",
                "Llamas de la banca: duran casi toda la mano y castigan fuerte a quien se queda quieto. A su dueño no lo tocan.",
                "The house's flames: they last most of the hand and punish anyone who stands still. They never touch their owner.");
            Content("tile.ice.name", "Hielo", "Ice");
            Content("tile.ice.desc",
                "Te deslizás en la dirección en la que entraste hasta salir del hielo o chocar.",
                "You slide in the direction you entered until you leave the ice or hit something.");
            Content("tile.portal.name", "Portal", "Portal");
            Content("tile.portal.desc",
                "Te teletransporta al portal conectado y te deja en la casilla siguiente.",
                "Teleports you to the linked portal and drops you on the next tile.");
            Content("tile.poison.name", "Veneno", "Poison");
            Content("tile.poison.desc",
                "Te envenena: daño al inicio de cada turno durante varios turnos.",
                "Poisons you: damage at the start of each turn for several turns.");
            Content("tile.heal.name", "Curación", "Healing");
            Content("tile.heal.desc",
                "Cura si TERMINÁS tu turno encima. Pasar de largo no cura.",
                "Heals if you END your turn on it. Passing through does nothing.");
            Content("tile.strength.name", "Fortaleza", "Strength");
            Content("tile.strength.desc",
                "Tus combos ofensivos pegan más fuerte mientras estés parado acá.",
                "Your offensive combos hit harder while you stand here.");
            Content("tile.boost.name", "Impulso", "Boost");
            Content("tile.boost.desc",
                "Bonifica tu próxima tirada de movimiento desde esta casilla.",
                "Boosts your next movement roll from this tile.");
            Content("tile.telegraph.name", "Advertencia", "Telegraph");
            Content("tile.telegraph.desc",
                "Algo va a pasar acá cuando termine la ronda. Mejor no estar.",
                "Something will happen here when the round ends. Best not to be around.");
            Content("tile.electricpuddle.name", "Charco Eléctrico", "Electric Puddle");
            Content("tile.electricpuddle.desc",
                "Te aturde un turno al entrar o al ser empujado encima.",
                "Stuns you for a turn when entered or pushed onto.");
            Content("tile.safezone.name", "Zona de Seguridad", "Safe Zone");
            Content("tile.safezone.desc",
                "Protege de efectos específicos a cualquier unidad adentro.",
                "Shields any unit inside from specific effects.");
        }

        // ==================================================================
        // Tooltips de los íconos del player
        // ==================================================================

        /// <remarks>
        /// Solo el nombre. El ícono ya dice qué es de un vistazo; el tooltip está para
        /// desambiguarlo, no para explicar lo que el panel que abre va a mostrar igual.
        /// </remarks>
        private static void SeedPlayerIcons()
        {
            Ui(PlayerIconTextKeys.Contract, "Contrato", "Contract");
            Ui(PlayerIconTextKeys.Backpack, "Inventario", "Inventory");
            Ui(PlayerIconTextKeys.DiceBag, "Bolsa de dados", "Dice bag");
        }

        // ==================================================================
        // Panel de la bolsa de dados
        // ==================================================================

        private static void SeedDiceBag()
        {
            Ui(DiceBagTextKeys.Title, "Bolsa de Dados", "Dice Bag");
            Ui(DiceBagTextKeys.SlotsCaption, "Cupos de encantamiento", "Enchantment slots");
            // Va detrás del contador ("2/3 cupos"), así que en minúscula y sin punto.
            Ui(DiceBagTextKeys.SlotsSuffix, "cupos", "slots");
            Ui(DiceBagTextKeys.EmptySlot, "Cupo libre.", "Empty slot.");
            Ui(DiceBagTextKeys.NoEnchantments, "Sin encantamientos.", "No enchantments.");
        }

        // ==================================================================
        // Drawer de contrato
        // ==================================================================

        private static void SeedContractDrawer()
        {
            // Encabezados de la tabla. El de daño se parte en dos líneas: la columna mide
            // 66 px y ninguna de las dos versiones entra de una.
            Ui(ContractTextKeys.HeaderExample, "Ejemplo", "Example");
            Ui(ContractTextKeys.HeaderName, "Combo", "Combo");
            Ui(ContractTextKeys.HeaderDamage, "Daño base", "Base DMG");
        }

        // ==================================================================
        // Reveal del cofre (Feature#0046)
        // ==================================================================

        private static void SeedChest()
        {
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.Title, "¡Cofre abierto!", "Chest opened!");
            // El {0} es el monto de oro — tiene que sobrevivir la traducción.
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.GoldAmount, "+{0} de oro", "+{0} gold");
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.SkipHint, "Click para acelerar", "Click to skip");
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.ContinueHint, "Click para continuar", "Click to continue");
            // Tiers con los nombres estándar de rareza del juego (pedido del usuario:
            // fuera los nombres custom del GDD). Términos de juego sin traducir —
            // mismo tratamiento que "Reroll"/"Combo".
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityCommon, "Common", "Common");
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityUncommon, "Uncommon", "Uncommon");
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityRare, "Rare", "Rare");
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityLegendary, "Legendary", "Legendary");
        }

        // ==================================================================
        // Drawer de inventario + items
        // ==================================================================

        /// <remarks>
        /// Los nombres/descripciones de los items van a la tabla Content con las keys de
        /// entidad (<c>&lt;ItemId&gt;.name</c> / <c>.desc</c>) — son del item, no del
        /// drawer que los muestra. El ES está normalizado a la guía de tono (tuteo
        /// neutro, tildes, punto final); el typo del id <c>bendicion.destinoo.generala</c>
        /// se conserva porque identifica a la entidad.
        /// </remarks>
        private static void SeedInventory()
        {
            Ui(InventoryTextKeys.Title, "Inventario", "Inventory");
            Ui(InventoryTextKeys.ItemsCaption, "Objetos", "Items");

            Item("amuleto.reflejo", "Amuleto de Reflejo", "Reflection Amulet",
                "Cuando recibes daño, le sacas 10 de vida al enemigo que te golpeó.",
                "When you take damage, deal 10 damage back to the enemy that hit you.");

            Item("banquete.full", "Banquete Real", "Royal Feast",
                "Los Full House hacen +30 daño.",
                "Full Houses deal +30 damage.");

            Item("bendicion.destinoo.generala", "Bendición del Destino", "Blessing of Fate",
                "Cada Generala cura 15 HP.",
                "Each Generala heals 15 HP.");

            Item("bolsa.comerciante.doblepar", "Bolsa del comerciante", "Merchant's Pouch",
                "Cada Doble Par da +5 oro.",
                "Each Double Pair grants +5 gold.");

            Item("botas.escaladores.escalera", "Botas del Escalador", "Climber's Boots",
                "La Escalera hace +25 daño.",
                "The Ladder deals +25 damage.");

            Item("botas.ligeras", "Botas Ligeras", "Light Boots",
                "+1 movimiento base.",
                "+1 base movement.");

            Item("cadena.comercial.doblepar", "Cadena comercial", "Trade Chain",
                "El Doble Par hace +20 de daño.",
                "Double Pair deals +20 damage.");

            Item("copa.generala", "Copa del Campeón", "Champion's Cup",
                "La Generala hace +50 daño.",
                "The Generala deals +50 damage.");

            Item("coraza.reforzada", "Coraza Reforzada", "Reinforced Plate",
                "+2 vida máxima.",
                "+2 max health.");

            Item("corona.noble.full", "Corona Noble", "Noble Crown",
                "Cada Full House recupera 5 HP.",
                "Each Full House restores 5 HP.");

            Item("egoista", "El egoísta", "The Selfish One",
                "Cuanto más oro tienes, más daño haces.",
                "The more gold you have, the more damage you deal.");

            Item("escudo.imperial.poker", "Escudo Imperial", "Imperial Shield",
                "Cada Póker genera +2 escudo.",
                "Each Poker grants +2 shield.");

            Item("espada.fuerzabruta", "Espada de Fuego", "Fire Sword",
                "El combo Fuerza Bruta hace +35 de daño.",
                "The Brute Force combo deals +35 damage.");

            Item("guantes.apostador.par", "Guantes del apostador", "Gambler's Gloves",
                "Los Pares hacen +20 de daño.",
                "Pairs deal +20 damage.");

            Item("guantes.maestro.poker", "Guantes del maestro", "Master's Gloves",
                "El Póker hace +25 daño.",
                "The Poker deals +25 damage.");

            Item("instinto.supervivencia", "Instinto de Supervivencia", "Survival Instinct",
                "Al bajar de 30% HP obtienes +1 escudo.",
                "When you drop below 30% HP you gain +1 shield.");

            Item("moneda.suerte.par", "Moneda de la suerte", "Lucky Coin",
                "Cada Par da +2 oro.",
                "Each Pair grants +2 gold.");

            Item("ritual.sangre.trio", "Ritual de Sangre", "Blood Ritual",
                "Cada Trío recupera 1 HP.",
                "Each Trio restores 1 HP.");

            Item("rodilleras.acero", "Rodilleras de Acero", "Steel Kneepads",
                "Tienes +3 de escudo al inicio de cada combate.",
                "You start each combat with +3 shield.");

            Item("talisman.vital", "Talismán Vital", "Vital Talisman",
                "Recuperas 1 HP al iniciar combate.",
                "Restore 1 HP when combat starts.");

            Item("tesoro.generala", "Tesoro Legendario", "Legendary Treasure",
                "Cada Generala da +30 oro.",
                "Each Generala grants +30 gold.");

            Item("totem.clan.trio", "Tótem del Clan", "Clan Totem",
                "Los Tríos hacen +30 de daño.",
                "Trios deal +30 damage.");

            // El .name de la poción lo siembra SeedContentBaseline; acá solo se suma
            // la descripción que le faltaba para el tooltip del inventario.
            Content("potion.healing.desc",
                "Recupera vida al usarla.",
                "Restores health when used.");
        }

        // ==================================================================
        // Chrome de menús y HUD (tabla UI) — promoción de keys solo-asset
        // ==================================================================

        /// <remarks>
        /// Estas keys vivían SOLO en los .asset de tabla (cargadas a mano en el commit
        /// i18n original, ninguna herramienta las escribía) — una regeneración de tablas
        /// desde el seeder las perdía, igual que BUG-026. Valores idénticos a los de las
        /// tablas, así que re-sembrar es idempotente.
        /// </remarks>
        private static void SeedMenuChrome()
        {
            // Menú principal.
            Ui("menu.play", "Jugar", "Play");
            Ui("menu.continue", "Continuar", "Continue");
            Ui("menu.unlocks", "Desbloqueos", "Unlocks");
            Ui("menu.delete", "Borrar partida", "Delete Save");
            Ui("menu.tutorial", "Tutorial", "Tutorial");
            Ui("menu.quit", "Salir", "Quit");
            Ui("menu.tutorial_on", "Tutorial: SÍ", "Tutorial: ON");
            Ui("menu.tutorial_off", "Tutorial: NO", "Tutorial: OFF");

            // Pantallas de desbloqueos / pausa / fin de run.
            Ui("screen.return_to_menu", "Volver al menú", "Return to Menu");
            Ui("unlocks.title", "DESBLOQUEOS", "UNLOCKS");
            Ui("toast.unlocked", "¡Desbloqueado!", "Unlocked!");
            Ui("pause.resume", "Reanudar", "Resume");
            Ui("pause.quit_run", "Abandonar run", "Quit Run");
            Ui("victory.title", "¡Victoria!", "Victory!");
            Ui("defeat.title", "Derrota", "Defeat");
            Ui("floor.continue", "Continuar", "Continue");
            Ui("floor.label", "Piso", "Floor");

            // HUD de gameplay: nav, acciones y tiro de acción.
            Ui("actionroll.roll", "Tirar", "Roll");
            Ui("nav.proceed", "Avanzar", "Proceed");
            Ui("nav.pause", "Pausa", "Pause");
            Ui("nav.rooms", "Salas", "Rooms");
            Ui("action.attack", "Atacar", "Attack");
            Ui("action.move", "Mover", "Move");
            Ui("action.special_attack", "Ataque especial", "Special Attack");
            Ui("action.force_door", "Forzar puerta", "Force Door");
            Ui("action.heal", "Curar", "Heal");
            Ui("action.end_turn", "Terminar turno", "End Turn");
            Ui("action.pass", "Pasar", "Pass");
            Ui("action.pass_door", "Cruzar puerta", "Pass Door");

            // Tipos de sala (nav del HUD).
            Ui("room.type.start", "Inicio", "Start");
            Ui("room.type.combat", "Combate", "Combat");
            Ui("room.type.boss", "Jefe", "Boss");
            Ui("room.type.shop", "Tienda", "Shop");
            Ui("room.type.potion", "Poción", "Potion");
            Ui("room.type.enchantment", "Encantamiento", "Enchantment");

            // Altar de encantamiento (layout viejo) + unidad de oro. Las keys del
            // rediseño (altar.title, altar.your_dice, …) las upsertea
            // EnchantmentAltarSetupTools — no duplicar acá.
            Ui("gold.unit", "oro", "gold");
            Ui("altar.cost", "Costo", "Cost");
            Ui("altar.current_faces", "Caras actuales", "Current faces");
            Ui("altar.dice_header", "Tus dados:", "Your dice:");
            Ui("altar.empty", "Vacío", "Empty");
            Ui("altar.enchanted", "Encantado", "Enchanted");
            Ui("altar.gold", "Oro", "Gold");
            Ui("altar.slot_header", "Cupos del dado seleccionado:", "Selected die slots:");
        }

        // ==================================================================
        // Baseline de Content — promoción de keys solo-asset
        // ==================================================================

        /// <remarks>
        /// Mismo caso que <see cref="SeedMenuChrome"/> pero para la tabla Content:
        /// héroe, combos del contrato, enemigos y lore de bosses que solo existían en
        /// los .asset. El lore de los bosses conserva su registro narrativo en tercera
        /// persona a propósito. <c>passive.warrior.low_hp_rage.*</c> NO va acá — lo
        /// upsertea <c>ClassSelectionSetupTools</c>.
        /// </remarks>
        private static void SeedContentBaseline()
        {
            // Héroe y su pasiva base.
            Content("Warrior.name", "Guerrero", "Warrior");
            Content("warrior_tutorial.name", "Guerrero (Tutorial)", "Warrior (Tutorial)");
            Content("passive.warrior.heal_on_turn.name",
                "Regeneración del Guerrero", "Warrior's Regeneration");
            Content("passive.warrior.heal_on_turn.desc",
                "Al inicio de cada turno, el guerrero se cura 2 HP.",
                "At the start of each turn, the warrior heals 2 HP.");

            // Combos del contrato. "Generala" y "Full House" son ES == EN a propósito
            // (IdenticalByDesign en LocalizationTablesTests).
            Content("combo.double_pair.name", "Doble Par", "Double Pair");
            Content("combo.double_pair.desc", "2 pares de valores distintos", "2 pairs of different values");
            Content("combo.ladder.name", "Escalera", "Straight");
            Content("combo.ladder.desc", "Dados en escalera", "Dice in a run");
            Content("combo.brute_force.name", "Fuerza Bruta", "Brute Force");
            Content("combo.brute_force.desc",
                "Suma los valores de todos los dados que cayeron en la mitad superior de su propio rango (d6: 4-6, d8: 5-8, d12: 7-12).",
                "Sums the values of all dice that landed in the upper half of their own range (d6: 4-6, d8: 5-8, d12: 7-12).");
            Content("combo.full_house.name", "Full House", "Full House");
            Content("combo.full_house.desc", "2 dados de un valor y 3 de otro", "2 dice of one value and 3 of another");
            Content("combo.generala.name", "Generala", "Generala");
            Content("combo.generala.desc", "5 dados del mismo valor", "5 dice of the same value");
            Content("combo.higher_number.name", "Número Mayor", "Higher Number");
            Content("combo.higher_number.desc", "Dado más alto", "Highest die");
            Content("combo.pair.name", "Par", "Pair");
            Content("combo.pair.desc", "2 dados del mismo valor", "2 dice of the same value");
            Content("combo.poker.name", "Póker", "Poker");
            Content("combo.poker.desc", "4 dados del mismo valor", "4 dice of the same value");
            Content("combo.trio.name", "Trío", "Trio");
            Content("combo.trio.desc", "3 dados del mismo valor", "3 dice of the same value");

            Content("potion.healing.name", "Poción", "Potion");

            // Enemigos.
            Content("Boss01.name", "Jefe de Prueba", "Boss Test");
            Content("healerEnemy.name", "Sanador", "Healer");
            Content("CardEnemy01.name", "Enemigo Carta", "Card Enemy");
            Content("RangedEnemy01.name", "Enemigo a Distancia", "Ranged Enemy");
            Content("RangedEnemy01.desc",
                "Goblin con ataque a distancia, dispara con una ballesta.",
                "A goblin with a ranged attack; fires a crossbow.");
            Content("enemy_tutorial_melee_b.name", "Recluta (Tutorial)", "Recruit (Tutorial)");
            Content("enemy_tutorial_melee_c.name", "Matón (Tutorial)", "Thug (Tutorial)");

            // Lore de bosses — 3ª persona narrativa, no pasarlo a tuteo.
            Content("boss.general_director.name", "Director General", "General Director");
            Content("boss.general_director.desc",
                "El más viejo de todos. Ha estado solo en el piso superior tanto tiempo " +
                "que ya no recuerda con certeza cuáles eran las reglas originales del " +
                "casino. En algún momento dejó de buscarlas en el manual y empezó a " +
                "inventarlas. Las hace cumplir con total convicción y plena autoridad " +
                "burocrática. Que beneficien o perjudiquen al jugador es apenas un " +
                "detalle administrativo que no influye en su juicio.",
                "The oldest of them all. He has been alone on the top floor for so long " +
                "that he no longer remembers for sure what the original casino rules " +
                "were. At some point, he stopped looking for them in the manual and " +
                "started making them up. He enforces them with complete conviction and " +
                "full bureaucratic authority. Whether they benefit or harm the player is " +
                "just an administrative detail that doesn't factor into his judgment.");
            Content("boss.security_boss.name", "Jefe de Seguridad", "Security Boss");
            Content("boss.security_boss.desc",
                "Ha pasado siglos revisando los protocolos de seguridad del casino. " +
                "Cada acción queda registrada en su libro de incidentes. Detectó a miles " +
                "de tramposos en su vida y sigue aplicando los mismos procedimientos " +
                "aunque no quede nadie vivo a quien proteger. Si el jugador repite una " +
                "jugada, él ya la tiene anotada.",
                "He has spent centuries reviewing casino security protocols. Every " +
                "action is recorded in his incident book. He detected thousands of " +
                "cheaters in his lifetime and continues applying the same procedures " +
                "even if there is no one left alive to protect. If the player repeats a " +
                "move, he already has it recorded.");
            Content("boss.sunken_grand.name", "El Gran Hundido", "The Sunken Grand");
            Content("boss.sunken_grand.desc",
                "Lleva siglos calculando probabilidades en el casino. No necesita " +
                "adivinar qué combo va a sacar el jugador: le basta con quitar una parte " +
                "de la ecuación. Habla durante el combate haciendo observaciones " +
                "estadísticas presumidas sobre la tirada del jugador. No es cruel, es un " +
                "actuario que nunca aprendió a callarse.",
                "He has been calculating odds in the casino for centuries. He doesn't " +
                "need to guess what combo the player is going to roll: it is enough for " +
                "him to take a part of the equation out of it. He speaks during combat " +
                "making cool statistical observations about the player's roll. He's not " +
                "cruel, he's an actuary who never learned to shut up.");
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static void Ui(string key, string es, string en)
            => LocalizationSetupTools.UpsertEntry(UiTable, key, es, en);

        private static void Content(string key, string es, string en)
            => LocalizationSetupTools.UpsertEntry(ContentTable, key, es, en);

        /// <summary>Atajo para el par <c>ench.&lt;id&gt;.name</c> / <c>.desc</c>.</summary>
        private static void Ench(string id, string nameEs, string nameEn, string descEs, string descEn)
        {
            Content($"ench.{id}.name", nameEs, nameEn);
            Content($"ench.{id}.desc", descEs, descEn);
        }

        /// <summary>Atajo para el par <c>&lt;ItemId&gt;.name</c> / <c>.desc</c> (sin prefijo:
        /// el ItemId ya identifica la entidad, igual que los combos y unlocks).</summary>
        private static void Item(string id, string nameEs, string nameEn, string descEs, string descEn)
        {
            Content($"{id}.name", nameEs, nameEn);
            Content($"{id}.desc", descEs, descEn);
        }
    }
}
