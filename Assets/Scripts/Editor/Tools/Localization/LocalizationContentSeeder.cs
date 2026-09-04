using UnityEditor;
using UnityEngine;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities.Traits;
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
            SeedTooltipEffects();
            SeedEnchantments();
            SeedUnlockHints();
            SeedMiscContent();
            SeedBuildHelp();
            SeedStatusIcons();
            SeedArchetypes();
            SeedAttackKinds();
            SeedSpecialTiles();
            SeedContractDrawer();
            SeedPlayerIcons();
            SeedDiceBag();
            SeedInventory();
            SeedChest();
            SeedMenuChrome();
            SeedContentBaseline();
            SeedPassiveItems();

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
                "Este es tu POOL DE ROLLS: cada tirada de dados consume 1. Al terminar tu turno recuperas {0} (máximo {1}).",
                "This is your ROLL POOL: every dice throw spends 1. Ending your turn grants {0} back ({1} max).");

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
                "Curar también usa los dados: arma un combo y recuperas más vida. Sin combo, cura tu dado más alto. Bloquea y confirma.",
                "Healing uses the dice too: form a combo to recover more health. No combo? Your highest die heals you. Lock and confirm.");

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

            // BUG-068: el paso gatea rotación + zoom — estas dos solo se muestran
            // mientras falta practicar el control pendiente.
            Ui(TutorialTextKeys.CameraNeedsRotate,
                "¡Hiciste zoom! Ahora gira la cámara: mantén el botón derecho y arrastra el mouse.",
                "You zoomed! Now rotate the camera: hold the right button and drag the mouse.");

            Ui(TutorialTextKeys.CameraNeedsZoom,
                "¡Giraste la cámara! Ahora prueba el zoom: usa la rueda del mouse para acercar o alejar.",
                "You rotated the camera! Now try the zoom: use the mouse wheel to zoom in or out.");

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
                "Elige un dado y tira de la palanca: aparecen 3 encantamientos y el que elijas se suma al dado.",
                "Pick a die and pull the lever: 3 enchantments appear and the one you pick is added to the die.");

            Ui(TutorialTextKeys.EnchantReroll,
                "¿Ninguno te convence? Tira de la palanca otra vez para ver 3 opciones nuevas: cada tirada cuesta más oro.",
                "None convince you? Pull the lever again for 3 new options: each roll costs more gold.");

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

            // Toast de Segundo Aliento sobre la pila de vida (SecondWindFeedbackView):
            // {0} = nombre del item consumido, {1} = HP con los que quedó el jugador.
            Ui(UiTextKeys.SecondWindTitle,
                "¡Segundo Aliento!",
                "Second Wind!");
            Ui(UiTextKeys.SecondWindBody,
                "{0} te dejó en {1} HP.",
                "{0} left you at {1} HP.");

            // Toast de item roto (ItemBrokeDownFeedbackView): Eco Menguante al agotar su
            // multiplicador. {0} = nombre del item.
            Ui(UiTextKeys.ItemBrokeDownTitle,
                "¡Se rompió!",
                "It broke!");
            Ui(UiTextKeys.ItemBrokeDownBody,
                "{0} agotó su poder y desapareció.",
                "{0} ran out of power and is gone.");

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
        // BUG-041: tooltips de acciones de hero (HeroActionTooltip + Eff*.BuildTooltip)
        // esquivaban la localización por completo — literales en código. Familia
        // tooltip.effect.<effect>.<variant>; tooltip.hero_action.* para el chrome del
        // header (nombre ya sale de action.* vía HeroActionTooltip.ResolveActionName).
        // ==================================================================
        private static void SeedTooltipEffects()
        {
            Ui("tooltip.hero_action.cost_per_roll",
                "Costo: 1 Roll por tirada",
                "Cost: 1 Roll per roll");

            // Sufijo de multiplicador (" × 1.5"): compartido por daño/curación/escudo
            // cuando el ComboMultiplier del effect no es 1. Símbolo + número puro, sin
            // palabras que traducir — mismo criterio que "Combo" (ver IdenticalByDesign
            // en LocalizationTablesTests).
            Ui("tooltip.effect.combo.multiplier_suffix", " × {0}", " × {0}");

            // Segunda línea de la fórmula N×M sin combo — idéntica en EffDealDamage y
            // EffHeal (ambas mezclan el dado holdeado más alto a la misma fórmula).
            Ui("tooltip.effect.combo.no_combo_fallback",
                "Sin combo: ATQ + dado más alto elegido",
                "No combo: ATK + highest kept die");

            Ui("tooltip.effect.damage.combo_header",
                "Daño: ATQ ({0}) + puntaje del combo",
                "Damage: ATK ({0}) + combo score");
            Ui("tooltip.effect.damage.flat", "Daño: {0}", "Damage: {0}");

            // Renglón extra de Forzar Puerta cuando hay picos u otros ítems sumando.
            Ui("tooltip.effect.force_door.item_bonus",
                "Bonus de objetos a tu tirada: +{0}",
                "Item bonus to your roll: +{0}");

            // Mecha de una bomba de sala en su tooltip de mundo.
            Ui("prop.tooltip.fuse", "Estalla en {0} turnos", "Explodes in {0} turns");

            Ui("tooltip.effect.heal.combo_header",
                "Curación: ATQ ({0}) + base del combo × multi de dados",
                "Healing: ATK ({0}) + combo base × dice multiplier");
            Ui("tooltip.effect.heal.combo_value",
                "Curación: puntaje del combo",
                "Healing: combo score");
            Ui("tooltip.effect.heal.percent",
                "Curación: {0}% del HP máximo",
                "Healing: {0}% of max HP");
            Ui("tooltip.effect.heal.flat", "Curación: {0} HP", "Healing: {0} HP");

            Ui("tooltip.effect.shield.combo_header",
                "Escudo: ATQ ({0}) + base del combo × multi de dados",
                "Shield: ATK ({0}) + combo base × dice multiplier");
            Ui("tooltip.effect.shield.flat", "Escudo: +{0}", "Shield: +{0}");
            // Feature#0055 — Habilidad de Clase: Empuje.
            Ui("tooltip.effect.push.header", "Empuje: casillas según el combo", "Push: tiles by combo");
            Ui("tooltip.effect.push.no_combo", "Sin combo: la tirada se pierde sin efecto",
                "No combo: the roll is spent with no effect");
            Ui("formula.push.preview", "{0}: empuja {1}", "{0}: push {1}");
            Ui("formula.push.no_combo", "Empuje - sin combo: sin efecto", "Push - no combo: no effect");

            Ui("tooltip.effect.force_door.boss_room",
                "El Boss debe ser vencido — no se puede forzar la puerta",
                "The Boss must be defeated — the door can't be forced");
            Ui("tooltip.effect.force_door.threshold", "Puntaje a superar: {0}", "Score to beat: {0}");

            Ui("tooltip.effect.move.default", "Moverse", "Move");
            Ui("tooltip.effect.move.global",
                "Moverse a cualquier casilla libre de la sala",
                "Move to any free tile in the room");
            Ui("tooltip.effect.move.range", "Moverse hasta {0} casillas", "Move up to {0} tiles");
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
                "Si guardás este dado entre tiradas, aporta +5 de daño por cada tirada que pase sin relanzarlo (máx +15). Se pierde al relanzarlo.",
                "If you hold this die between rolls, it adds +5 damage per roll it stays held (max +15). Lost when rerolled.");

            Ench("avaro", "Avaro", "Miser",
                "Otorga 5 de oro al completar un trío, póker o generala.",
                "Grants 5 gold when you complete a trio, four of a kind or five of a kind.");

            Ench("caras_centrales", "Caras Centrales", "Middle Faces",
                "Bloquea el cuarto superior e inferior. d8:{3,4,5,6} d12:{4,5,6,7,8,9}",
                "Blocks the top and bottom quarter. d8:{3,4,5,6} d12:{4,5,6,7,8,9}");

            Ench("cargado", "Cargado", "Loaded",
                "Una vez por combate puedes volver a tirar este dado y quedarte con el mayor resultado.",
                "Once per combat you can reroll this die and keep the higher result.");

            Ench("codicioso", "Codicioso", "Covetous",
                "El dado participa en una Escalera y suma +5 de oro.",
                "The die joins a Ladder and grants +5 gold.");

            Ench("el_caudal", "El Caudal", "Windfall",
                "El dado participa en un Doble Par y suma +3 de oro.",
                "The die joins a Double Pair and grants +3 gold.");

            Ench("comodin", "Comodín", "Wild",
                "Este dado cuenta como cualquier número para propósitos de combo (escaleras, trío, generala).",
                "This die counts as any number for combo purposes (straights, trio, generala).");

            Ench("escalador", "Escalador", "Climber",
                "Para escaleras, este dado cuenta como su valor y como valor+1 simultáneamente.",
                "For straights, this die counts as both its value and its value+1 at the same time.");

            Ench("escudado", "Escudado", "Shielded",
                "Si este dado participa en un póker, generás 15 puntos de escudo.",
                "If this die takes part in a four of a kind, you gain 15 shield.");

            Ench("extremos", "Extremos", "Extremes",
                "Solo muestra el cuarto superior e inferior. d8:{1,2,7,8} d12:{1,2,3,10,11,12}",
                "Only shows the top and bottom quarter. d8:{1,2,7,8} d12:{1,2,3,10,11,12}");

            Ench("fortaleza", "Fortaleza", "Fortress",
                "Si este dado saca su valor máximo y participa en un combo, generás 30 puntos de escudo.",
                "If this die rolls its maximum and takes part in a combo, you gain 30 shield.");

            Ench("fragil", "Frágil", "Fragile",
                "En cada tirada, 50% de que este dado no sume daño y 50% de que sume el doble. Sigue contando para formar el combo.",
                "Each roll, 50% chance this die adds no damage and 50% it adds double. It still counts toward forming the combo.");

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
                "Este dado no se puede guardar entre tiradas: siempre se relanza. A cambio, suma +5 de daño cuando participa en un combo.",
                "This die can't be held between rolls: it always rerolls. In exchange it adds +5 damage when it takes part in a combo.");

            Ench("mercader", "Mercader", "Merchant",
                "Si este dado participa en una generala, otorga 12 de oro.",
                "If this die takes part in a five of a kind, it grants 12 gold.");

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
                "Este dado no suma daño, pero cada vez que participa en un combo genera +2 de oro.",
                "This die adds no damage, but each time it takes part in a combo it generates +2 gold.");

            Ench("par", "Par", "Even",
                "Solo muestra caras pares. d6:{2,4,6} d8:{2,4,6,8} d12:{2,4,6,8,10,12}",
                "Only shows even faces. d6:{2,4,6} d8:{2,4,6,8} d12:{2,4,6,8,10,12}");

            Ench("parity_gamble", "Apuesta de Paridad", "Parity Gamble",
                "Si el dado saca un número impar multiplica x3; si no, x0.",
                "If this die rolls an odd number it multiplies x3; otherwise x0.");

            Ench("pesado", "Pesado", "Heavy",
                "Este dado aporta +2 de daño cuando participa en un combo.",
                "This die adds +2 damage when it takes part in a combo.");

            Ench("primo", "Primo", "Prime",
                "Solo muestra números primos. d12:{2,3,5,7,11}",
                "Only shows prime numbers. d12:{2,3,5,7,11}");

            Ench("resonante", "Resonante", "Resonant",
                "Si 2 o más dados muestran el mismo número en la tirada final, este dado suma su valor dos veces al combo.",
                "If 2 or more dice show the same number in the final roll, this die adds its value to the combo twice.");

            Ench("sediento", "Sediento", "Thirsty",
                "Cada vez que participa en un combo consume 2 de oro y suma +0,2 al multiplicador. Sin oro, el combo no hace daño.",
                "Each time it joins a combo it consumes 2 gold and adds +0.2 to the multiplier. With no gold, the combo deals no damage.");

            Ench("torpe", "Torpe", "Clumsy",
                "Obliga a volver a tirar todos los dados en el turno 2 del combate.",
                "Forces a full reroll on turn 2 of the combat.");

            Ench("volatil", "Volátil", "Volatile",
                "Al sacar el máximo, este dado aporta el doble de daño. Con cualquier otra cara aporta la mitad.",
                "On its maximum this die deals double damage. On any other face it deals half.");

            // Feature#0073 — encantamientos del GDD que faltaban en el catálogo.
            Ench("vampiro", "Vampiro", "Vampire",
                "Cada vez que este dado participa en un combo, perdés 5 de vida y el multiplicador sube +0,3. Con 5 de vida o menos, el dado no suma daño.",
                "Each time this die takes part in a combo you lose 5 health and the multiplier rises by +0.3. At 5 health or less the die adds no damage.");

            Ench("solitario", "Solitario", "Loner",
                "Si este dado queda fuera del combo que jugás, genera +2 de oro.",
                "If this die is left out of the combo you play, it generates +2 gold.");

            Ench("enfiestado", "Enfiestado", "Party Animal",
                "Con cara impar, este dado aporta el triple de daño. Con cara par no aporta daño. Sigue contando para formar el combo.",
                "On an odd face this die deals triple damage. On an even face it deals none. It still counts toward forming the combo.");

            Ench("racha", "Racha", "Streak",
                "Por cada combo consecutivo en el que participe en este combate, aporta +3 de daño más (+3, +6, +9…). Se reinicia si queda fuera de un combo.",
                "For each consecutive combo it takes part in this combat, it adds +3 more damage (+3, +6, +9…). Resets if it's left out of a combo.");

            Ench("ejecutor", "Ejecutor", "Executioner",
                "Si este dado participa en un combo contra un enemigo con 25% de vida o menos, aporta +12 de daño.",
                "If this die takes part in a combo against an enemy at 25% health or less, it adds +12 damage.");
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
            // BUG-85: Energy pasó a subir el pool (máximo + arranque de combate).
            Content("char_rew.energy_plus_1.name", "Pool de rolls +1", "Roll pool +1");
            Content("char_rew.energy_plus_1.desc",
                "+1 al pool de rolls: sube el máximo y los rolls con los que arrancás cada combate.",
                "+1 to the roll pool: raises the max and the rolls you start each combat with.");
            // El asset se llama HP_Plus5 pero su id real es hp_plus_25 (da 25).
            Content("char_rew.hp_plus_25.name", "Vida máxima +25", "Max Health +25");
            // BUG-85: Speed+ se re-autoró como Movimiento+ (MoveRange).
            Content("char_rew.move_plus_1.name", "Movimiento +1", "Movement +1");
            Content("char_rew.move_plus_1.desc",
                "+1 celda de rango al dado de Movimiento en combate.",
                "+1 cell of range on the Movement die in combat.");

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
                "puedes repetir el mismo tipo tantas veces como quieras.",
                "These are your class dice. Click one to add it to your bag; you can repeat " +
                "the same type as many times as you like.");

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

            // Cooldown post-portal (con turnos, tickea como veneno/stun).
            Content("status.tp_delay.name", "Recién teletransportado", "Teleport Fatigue");
            Content("status.tp_delay.desc",
                "No podés volver a usar un portal hasta que pase el efecto.",
                "You cannot use a portal again until this wears off.");

            // La frase tactica del panel: como pelea, en una linea. Distinta del .desc (el lore
            // del parrafo) a proposito — el panel no lleva lore.
            Content("boss.croupier.brief",
                "Quema la sala y se teletransporta cuando lo alcanzás.",
                "Burns the room and teleports when you reach him.");
            Content("boss.cashier.brief",
                "Te tira tres casillas y te cobra oro en cada empujón. Lo que dejes vencer en el piso lo perdés.",
                "Throws you three tiles and charges you gold on every shove. Whatever you let expire on the floor is lost.");
            Content("boss.la_generala.brief",
                "Suelta anillos eléctricos en oleadas, con huecos entre anillo y anillo.",
                "Unleashes electric rings in waves, with gaps between the rings.");
            // El refuerzo del Cajero es un ranged común más: misma identidad que RangedEnemy01
            // a propósito — un nombre propio le prometía al jugador una mecánica que no tiene. La
            // frase es la MISMA, palabra por palabra, y ahora es cierta de los dos: su árbol
            // también se despega cuando lo tenés encima.
            Content("minion.cajero_comision.brief",
                "Dispara de lejos y se aleja cuando te le acercás.",
                "Shoots from afar and backs away when you get close.");
            Content("obj.dado_casa.brief",
                "No ataca. Cada dado roto le saca un combo a la Generala.",
                "It doesn't attack. Every die broken takes a combo away from the Generala.");

            // La bomba del Croupier (RoomObjectTooltipInfo arma su panel con estas keys). La frase
            // dice lo único que ningún número del panel puede decir: que romperla le saca las
            // casillas marcadas al paño.
            Content("roomobj.croupier.bomba.name", "Bomba", "Bomb");
            Content("roomobj.croupier.bomba.desc",
                "Rompela y se lleva sus casillas marcadas.",
                "Break it and its marked tiles go with it.");
            Content("healerEnemy.brief",
                "Cura a los suyos antes que pegarte.",
                "Heals its allies before it hits you.");
            Content("CardEnemy01.brief",
                "Corre hacia vos y pega de frente.",
                "Runs straight at you and swings up close.");
            Content("RangedEnemy01.brief",
                "Dispara de lejos y se aleja cuando te le acercás.",
                "Shoots from afar and backs away when you get close.");
            Content("ChestMimic01.brief",
                "Se hace pasar por cofre y pega cuando te acercás.",
                "Poses as a chest and strikes when you get close.");
            Content("enemy_tutorial_melee_b.brief", "Pega de cerca.", "Hits up close.");
            Content("enemy_tutorial_melee_c.brief", "Pega de cerca.", "Hits up close.");

            // Maldiciones de jefe (bloque PLAYER CURSE). La del Croupier reusa status.dice_block.
            Content("curse.bank_keeps.name", "La banca no espera", "The Bank Doesn't Wait");
            Content("curse.bank_keeps.desc",
                "Lo que dejás vencer en el piso se pierde.",
                "Whatever you let expire on the floor is lost.");
            Content("curse.repeat_ban.name", "Mano vetada", "Banned Hand");
            Content("curse.repeat_ban.desc",
                "No podés repetir el combo que acabás de anotar.",
                "You can't repeat the combo you just scored.");

            Content("enemy.fire_tiles.name", "Casillas de fuego", "Fire Tiles");
            // {0} = daño al entrar, {1} = al empezar el turno encima — la misma pareja que
            // status.burn.desc, porque son las dos caras del mismo fuego.
            Content("enemy.fire_tiles.desc",
                "<b>{0}</b> al entrar en una casilla. <b>{1}</b> si empezás tu turno sobre ella.",
                "<b>{0}</b> on entering a tile. <b>{1}</b> if you start your turn on it.");

            Content("ability.teleport.name", "Se teletransporta", "Teleport");
            Content("ability.teleport.desc",
                "Salta a una casilla al lado tuyo, o al otro lado de la sala.",
                "Jumps to a tile beside you, or across the room.");

            // Estados "parado sobre" (sin turnos: duran lo que dure la estadía en la casilla).
            Content("status.burn.name", "Quemadura", "Burn");
            // {0} = daño al entrar, {1} = daño al empezar el turno encima. Los pasa
            // TileStandStatusProvider.BurnState desde la definición que se está pisando: cuatro
            // fuegos comparten el tipo y cobran 8/12, 6/10 y 15/15. El <b> va en la tabla para
            // que la énfasis sea autorable por idioma.
            Content("status.burn.desc",
                "<b>{0}</b> al entrar en una casilla. <b>{1}</b> si empezás tu turno sobre ella.",
                "<b>{0}</b> on entering a tile. <b>{1}</b> if you start your turn on it.");
            Content("status.dice_block.name", "Candado", "Padlock");
            Content("status.dice_block.desc",
                "Uno de tus dados queda trabado. Sortea otro cada turno.",
                "One of your dice stays jammed. He draws another one every turn.");
            Content("status.tile_heal.name", "Casilla de Curación", "Healing Tile");
            Content("status.tile_heal.desc",
                "Terminá tu turno acá para recuperar vida.",
                "End your turn here to recover health.");
            Content("status.tile_speed.name", "Impulso", "Boost");
            Content("status.tile_speed.desc",
                "Esta casilla mejora tu próximo movimiento.",
                "This tile improves your next movement.");
            Content("status.tile_attack.name", "Fortaleza", "Strength");
            Content("status.tile_attack.desc",
                "Tus combos ofensivos hacen daño extra mientras permanezcas acá.",
                "Your offensive combos deal bonus damage while you stay here.");

            SeedIntents();
        }

        /// <summary>
        /// Lo que un enemigo va a hacer, en la tarjeta que sale al pasarle el mouse.
        /// </summary>
        /// <remarks>
        /// Todas las reglas se formatean con la misma terna —{0} daño, {1} cantidad, {2} turnos—
        /// y cada frase usa los que le sirven (ver <c>AIIntentText</c>). Las keys viven en
        /// <c>AIIntentTextKeys.All</c> y hay un test que exige que todas estén acá: sin entry, la
        /// tarjeta salía con el texto de autor en español aunque el juego corriera en inglés.
        /// </remarks>
        private static void SeedIntents()
        {
            Content(AIIntentTextKeys.Ignite + ".name", "Bola de fuego", "Fire Ball");
            // Una frase y sin números: la forma. El dónde lo marca el piso y los números del
            // fuego los lleva la tarjeta de Fire Tiles.
            Content(AIIntentTextKeys.Ignite + ".desc",
                "Prende un cono de fuego.",
                "Lights a cone of fire.");

            // Descripcion vacia a proposito, igual que el estallido: el titulo dice que hace, el
            // numero de la tarjeta dice cuanto, y "desde lejos" lo dice la familia del bicho.
            // El Pleno del Croupier: mismo nodo que la bola de fuego, otra pregunta. De la bola te
            // corrés al costado; de esto sólo se sale llegando al hueco.
            Content(AIIntentTextKeys.BurnRoom + ".name", "Pleno y color", "Full House Burn");
            Content(AIIntentTextKeys.BurnRoom + ".desc",
                "Prende la sala entera menos lo que rodea al jefe.",
                "Lights the whole room except what surrounds him.");

            Content(AIIntentTextKeys.RangedShot + ".name", "Disparo", "Shot");
            Content(AIIntentTextKeys.RangedShot + ".desc", string.Empty, string.Empty);

            Content(AIIntentTextKeys.BombField + ".name", "Bombas", "Bombs");
            Content(AIIntentTextKeys.BombField + ".desc",
                "Siembra <b>{1}</b> bombas al azar.",
                "Spawns <b>{1}</b> random bombs.");

            // Descripción vacía a propósito: el título dice qué pasa y el badge cuánto falta.
            // La entry existe igual porque el guard la exige y para poder llenarla sin tocar código.
            Content(AIIntentTextKeys.BombBlast + ".name", "Detonar la bomba", "Detonate the Bomb");
            Content(AIIntentTextKeys.BombBlast + ".desc", string.Empty, string.Empty);

            // Descripciones vacías: el título dice qué pasa y el número de la tarjeta cuánto.
            // Las casillas marcadas del golpe telegrafiado ya se ven en el paño al hoverear.
            Content(AIIntentTextKeys.Telegraph + ".name", "Golpe marcado", "Marked Strike");
            Content(AIIntentTextKeys.Telegraph + ".desc", string.Empty, string.Empty);

            Content(AIIntentTextKeys.Attack + ".name", "Golpe", "Strike");
            Content(AIIntentTextKeys.Attack + ".desc", string.Empty, string.Empty);

            Content(AIIntentTextKeys.CashierShove + ".name", "Empujón", "Shove");
            Content(AIIntentTextKeys.CashierShove + ".desc",
                "Te empuja <b>{1}</b> casillas y te cobra parte del oro que lleves encima.",
                "Shoves you <b>{1}</b> tiles and takes a cut of the gold you carry.");

            Content(AIIntentTextKeys.CashierVault + ".name", "Se la lleva la caja", "The Vault Takes It");
            Content(AIIntentTextKeys.CashierVault + ".desc", string.Empty, string.Empty);

            Content(AIIntentTextKeys.CashierCoins + ".name", "Monedas venciendo", "Coins Expiring");
            Content(AIIntentTextKeys.CashierCoins + ".desc",
                "La caja se lleva una por turno; quedan <b>{1}</b> en el piso.",
                "The vault takes one per turn; <b>{1}</b> left on the floor.");

            // Dos keys y no una: el turno que avisa y el que cobra dicen cosas distintas.
            Content(AIIntentTextKeys.CashierSlam + ".name", "Cañonazo", "Cannon Shot");
            Content(AIIntentTextKeys.CashierSlam + ".desc",
                "Marca un área de 3×3 donde estés parado y la cobra al turno siguiente.",
                "Marks a 3×3 area where you stand and fires on it next turn.");

            Content(AIIntentTextKeys.CashierSlamDue + ".name", "Cañonazo", "Cannon Shot");
            Content(AIIntentTextKeys.CashierSlamDue + ".desc",
                "Cae en el área marcada, no donde estés.",
                "It lands on the marked area, not on where you are.");

            Content(AIIntentTextKeys.Leaves + ".name", "Lo que deja", "What It Leaves");
            Content(AIIntentTextKeys.Leaves + ".desc",
                "Deja fuego: <b>{0}</b> al entrar, <b>{1}</b> por turno, {2} rondas.",
                "Leaves fire: <b>{0}</b> on entering, <b>{1}</b> per turn, for {2} rounds.");
        }

        /// <summary>
        /// La familia de combate que el panel de un enemigo dice de él (tabla UI).
        /// </summary>
        /// <remarks>
        /// El prefijo del jefe es un formato y no una concatenación en código: así el separador
        /// —y el orden, si algún idioma lo quiere al revés— es autorable por locale.
        /// </remarks>
        private static void SeedArchetypes()
        {
            Ui(EnemyArchetypeKeys.Melee, "Cuerpo a cuerpo", "Melee");
            Ui(EnemyArchetypeKeys.Ranged, "Rango", "Ranged");
            Ui(EnemyArchetypeKeys.Support, "Soporte", "Support");

            Ui(EnemyArchetypeKeys.Boss, "Jefe", "Boss");
            Ui(EnemyArchetypeKeys.BossFormat, "Jefe · {0}", "Boss · {0}");

            // La fecha del ataque en su tarjeta del costado. En chico y arriba del título: es lo
            // que lo distingue de lo que el jefe mantiene en el paño, que no lleva fecha.
            Ui(EnemyStatusIconsView.NextTurnKey, "Próximo turno", "Next turn");

            // La etiqueta del bloque de maldición del jefe, misma letra chica que la fecha.
            Ui(EnemyStatusIconsView.PlayerCurseKey, "Maldición", "Player Curse");
        }

        /// <summary>
        /// El tipo de ataque que la tarjeta de próximo turno suma al título (tabla UI).
        /// </summary>
        private static void SeedAttackKinds()
        {
            Ui(AttackKindTextKeys.ComboAttack, "Combo", "Combo");
            Ui(AttackKindTextKeys.BasicAttack, "Básico", "Basic");
            Ui(AttackKindTextKeys.DamageOverTime, "Daño sostenido", "Damage Over Time");
            // Vacía a propósito: "Ambiental" en el título de un ataque no califica nada que
            // el jugador pueda usar; la entry existe para poder llenarla sin tocar código.
            Ui(AttackKindTextKeys.Environmental, string.Empty, string.Empty);
            Ui(AttackKindTextKeys.Reaction, "Reacción", "Reaction");
            Ui(AttackKindTextKeys.ScriptedAbility, "Habilidad", "Ability");

            // Formato y no concatenación: el separador es autorable por locale, igual que el
            // prefijo del jefe.
            Ui(AttackKindTextKeys.TitleFormat, "{0} · {1}", "{0} · {1}");
        }

        /// <summary>
        /// Re-siembra SOLO las familias. Mismo criterio que <see cref="SeedStatusIconsOnly"/>:
        /// acotar el blast radius del seeder completo.
        /// </summary>
        [MenuItem("Rollgeon/Localization/Seed Enemy Archetypes")]
        public static void SeedArchetypesOnly()
        {
            SeedArchetypes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LocalizationContentSeeder] Familias de enemigo pobladas en la tabla UI.");
        }

        /// <summary>
        /// Re-siembra SOLO las claves de la fila de estados. Mismo criterio que
        /// <see cref="SeedSpecialTilesOnly"/>: acotar el blast radius del seeder completo.
        /// </summary>
        [MenuItem("Rollgeon/Localization/Seed Status Icons")]
        public static void SeedStatusIconsOnly()
        {
            SeedStatusIcons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LocalizationContentSeeder] Claves de la fila de estados pobladas.");
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

            // El panel estructurado de una casilla: la fila de tipo del header y las etiquetas
            // de sus tarjetas de números (los números viajan como dato, no en el texto).
            Ui("tile.category.format", "Casilla · {0}", "Tile · {0}");
            Ui("tile.category.damage", "Daño", "Damage");
            Ui("tile.category.heal", "Curación", "Healing");
            Ui("tile.category.status", "Estado", "Status");
            Ui("tile.category.buff", "Mejora", "Buff");
            Ui("tile.category.slide", "Deslizamiento", "Slide");
            Ui("tile.category.teleport", "Teletransporte", "Teleport");
            Ui("tile.category.warning", "Advertencia", "Warning");
            Ui("tile.category.protection", "Protección", "Protection");
            Ui("tile.panel.effect", "Efecto", "Effect");
            Ui("tile.panel.enter", "Al entrar", "On enter");
            Ui("tile.panel.turn_start", "Empezar el turno encima", "Start your turn on it");
            Ui("tile.panel.heal", "Cura al terminar el turno", "Heals at end of turn");
            Ui("tile.panel.applies", "Aplica", "Applies");

            // La caja EN EL PISO del panel de un enemigo parado sobre una casilla especial, y el
            // header de los objetos que un jefe pone en la sala (bombas).
            Ui("enemy.panel.on_the_floor", "En el piso", "On the floor");
            Ui("prop.panel.type", "Objeto", "Object");
            Ui("prop.panel.leaves", "Deja", "Leaves");

            // Los dos bloques de la bomba: el plazo arriba (donde el jugador ya busca el próximo
            // turno) y lo que hace al estallar abajo, como caja propia.
            Ui("prop.panel.fuse_tick", "Se acorta la mecha", "The fuse burns down");
            Ui("prop.panel.fuse_blows", "Explota", "It explodes");
            Ui("prop.panel.on_blast", "Al explotar", "On explosion");
            Ui("prop.panel.blast_hit", "Golpe del estallido", "Blast hit");

            // El panel de un hazard de sala (lluvia, escarcha, fichas): fila de tipo, etiqueta
            // del golpe y la cadencia del pie.
            Ui("hazard.panel.type", "Peligro de sala", "Room hazard");
            Ui("hazard.panel.hit", "Golpe", "Hit");
            Ui("hazard.panel.cycle", "Golpea cada {0} rondas", "Strikes every {0} rounds");
            Ui(Rollgeon.Combat.Threat.HazardTooltipInfo.ClockTicksKey, "Se vence", "Expiring");
            Ui(Rollgeon.Combat.Threat.HazardTooltipInfo.ClockDueKey,
                "Se la lleva la caja", "The vault takes it");

            // Identidad por hazard. El stun del hielo lo aplica IceStunBinder (Damage 0 en el
            // SO), por eso la frase habla de aturdir sin nombrar números.
            Content("hazard.fire.name", "Fuego", "Fire");
            Content("hazard.fire.desc",
                "Quema a quien se queda adentro.",
                "Burns whoever stays inside.");
            Content("hazard.frost.name", "Escarcha", "Frost");
            Content("hazard.frost.desc",
                "Hielo del cubilete: pisarlo aturde, y el parche se rompe con la pisada.",
                "Ice from her cup: stepping on it stuns, and the patch breaks underfoot.");
            Content("hazard.chip.name", "Ficha de la banca", "House Chip");
            Content("hazard.chip.desc",
                "Pisala y cobrás su valor. La que vence se pierde.",
                "Step on it to collect its value. One that expires is lost.");
            Content("hazard.table_fire.name", "Fuego de mesa", "Table Fire");
            Content("hazard.table_fire.desc",
                "Arde: golpea a quien termine su turno adentro.",
                "Burning: strikes whoever ends their turn inside.");
            Content("hazard.ice_trail.name", "Estela de hielo", "Ice Trail");
            Content("hazard.ice_trail.desc",
                "Pisarla aturde, y cada parche se rompe con la pisada.",
                "Stepping on it stuns, and each patch breaks underfoot.");
            Content("hazard.rain.name", "Lluvia", "Rain");
            Content("hazard.rain.desc",
                "Marca zonas y castiga al ciclo siguiente a quien siga adentro.",
                "Marks zones and punishes whoever is still inside next cycle.");

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
                "Llamas de la banca: duran casi toda la mano y castigan fuerte a quien se queda quieto. Al Croupier también lo queman.",
                "The house's flames: they last most of the hand and punish anyone who stands still. They burn the Croupier too.");
            Content("tile.fireartillery.name", "Fuego de Artillería", "Artillery Fire");
            Content("tile.fireartillery.desc",
                "Rescoldo del obús de la Artillería: quema fuerte al caer y sigue ardiendo cada turno encima.",
                "Embers from the Artillery's shell: burns hard on impact and keeps burning each turn standing on it.");
            Content("tile.firecroupierbomba.name", "Fuego de Bomba", "Bomb Fire");
            Content("tile.firecroupierbomba.desc",
                "Lo que deja una bomba al estallar. Quema mucho más que el fuego del paño, y tampoco perdona al Croupier.",
                "What a bomb leaves when it blows. It burns far worse than the house fire, and it doesn't spare the Croupier either.");
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
                "Cura si <b>terminás</b> tu turno encima. Pasar de largo no cura.",
                "Heals if you <b>end</b> your turn on it. Passing through does nothing.");
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
            // Key propia del pincho del Cajero: pega 20 contra los 12 del común, y el tooltip
            // no puede presentarlo como el mismo objeto.
            Content("tile.spikes_cajero.name", "Pinchos del Cajero", "Cashier's Spikes");
            Content("tile.spikes_cajero.desc",
                "Cobran cada vez que los cruzás: no se gastan. Duelen bastante más que los comunes.",
                "They charge every time you cross them - they never wear out. They hurt a fair bit more than the common ones.");
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
            Ui(DiceBagTextKeys.NoEnchantments, "Sin encantamientos.", "No enchantments.");

            // Labels de categoría del acordeón ("Ancla - Control").
            Ui(DiceBagTextKeys.CatAtaque, "Ataque", "Attack");
            Ui(DiceBagTextKeys.CatControl, "Control", "Control");
            Ui(DiceBagTextKeys.CatDefensa, "Defensa", "Defense");
            Ui(DiceBagTextKeys.CatEconomia, "Economía", "Economy");
            Ui(DiceBagTextKeys.CatMaldicion, "Maldición", "Curse");
            // Taxonomía GDD 2026-09 (Defensa/Economía/Maldición quedan como legacy).
            Ui(DiceBagTextKeys.CatCaos, "Caos", "Chaos");
            Ui(DiceBagTextKeys.CatRecursos, "Recursos", "Resources");
            Ui(DiceBagTextKeys.CatMovimiento, "Movimiento", "Movement");
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

            // El tooltip del cofre de mundo. UNA sola identidad para el cofre real y el mímico
            // camuflado — cualquier texto que los distinga convierte el hover en detector.
            Content("chest.name", "Cofre", "Chest");
            Content("chest.desc",
                "Rompelo y fijate qué guarda.",
                "Break it open and see what it holds.");
            // God (item-editor-spec.md §5.1): mismo criterio que sus hermanos — término
            // de juego sin traducir. El re-run de este seeder queda pendiente del gate.
            Ui(Rollgeon.UI.ChestReveal.ChestRevealTextKeys.RarityGod, "God", "God");
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

            // Feature#0074 — Tarjeta / Rezagado / Peaje.
            Item("tarjeta.de.credito", "Tarjeta de Crédito", "Credit Card",
                "Tu oro puede bajar hasta -30: comprá a crédito y pagá la deuda con lo que ganes.",
                "Your gold can drop to -30: buy on credit and pay the debt back with what you earn.");

            Item("rezagado", "Rezagado", "Straggler",
                "Al adquirirlo se fija en tu combo menos usado: ese combo hace +50% de daño el resto de la run.",
                "On pickup it locks onto your least-used combo: that combo deals +50% damage for the rest of the run.");

            Item("peaje", "Peaje", "Toll",
                "Al entrar a una sala de combate normal podés pagar 15 + 10 por piso de oro para limpiarla sin pelear. Sin botín.",
                "When entering a regular combat room you may pay 15 + 10 per floor in gold to clear it without fighting. No loot.");

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

            // Feature#0065 lo pasó de escudo a daño base — el seed tiene que decir lo mismo
            // que el ItemSO o re-seedear pisa la tabla con el texto viejo.
            Item("instinto.supervivencia", "Instinto de Supervivencia", "Survival Instinct",
                "Cuando tu vida está en 30 o menos, tu daño base aumenta en 5 puntos.",
                "When your health is 30 or less, your base damage increases by 5.");

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
            // Feature#0055: el slot 2 pasó de Ataque especial a Habilidad de Clase (genérica por
            // clase — el tooltip del efecto lleva el detalle "Empuje").
            Ui("action.class_skill", "Habilidad de clase", "Class Skill");
            Ui("action.force_door", "Forzar puerta", "Force Door");
            Ui("action.heal", "Curar", "Heal");
            // BUG-041: falta del slot Defense en la familia action.* — HeroActionTooltip
            // mapea Slot → key para localizar el nombre de la acción en hover.
            Ui("action.defense", "Defensa", "Defense");
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
            Content("healerEnemy.desc",
                "Un espíritu de la casa que remienda a los suyos.",
                "A house spirit that patches up its own.");
            Content("CardEnemy01.name", "Enemigo Carta", "Card Enemy");
            Content("CardEnemy01.desc",
                "Soldado de la casa, cuerpo a cuerpo.",
                "House soldier; fights up close.");
            Content("ChestMimic01.name", "Mímico", "Mimic");
            Content("ChestMimic01.desc",
                "Un cofre que muerde.",
                "A chest that bites.");
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

            // Los tres del pool y su elenco. Acá NO va lore: es el texto del pergamino de
            // hover, la única lectura que el jugador tiene antes de morir, así que dice lo que
            // el jefe HACE y entra de un vistazo. Los tres de arriba son lore largo porque
            // están en el banco y nadie los pasa el mouse por encima.
            Boss(CroupierAssetBuilder.EntityId,
                "El Croupier", "The Croupier",
                "Siembra bombas, prende el suelo y dispara de lejos. Acorralalo y se va al borde.",
                "Sows bombs, lights the floor and shoots from range. Crowd him and he bolts for the edge.");

            Boss(CajeroAssetBuilder.EntityId,
                "El Cajero", "The Cashier",
                "Te tira lejos y te saca oro, y parte de esa plata cae al piso. La que no levantás a tiempo se pierde.",
                "Throws you clear and takes your gold, and some of it lands on the floor. Whatever you don't grab in time is gone.");

            // Misma identidad que RangedEnemy01 a propósito: el refuerzo del Cajero es un
            // ranged común más, no un personaje.
            Boss(CajeroAssetBuilder.CritterEntityId,
                "Enemigo a Distancia", "Ranged Enemy",
                "Goblin con ataque a distancia, dispara con una ballesta.",
                "A goblin with a ranged attack; fires a crossbow.");

            Boss(GeneralaAssetBuilder.BossEntityId,
                "La Generala", "The Generala",
                "Tira su propia mano a la vista y te cobra la categoría que le salga. Rompele un dado y le borrás una.",
                "Rolls her hand in the open and charges you whatever category it lands. Break a die and you erase one.");

            Boss(GeneralaAssetBuilder.DiceEntityId,
                "Dado de la Casa", "House Die",
                "Un dado gigante de la mano de la Generala.",
                "A giant die from the Generala's hand.");

            // Bestiario nuevo (salas del 03/09). Frases cortas de hover: dicen lo que
            // el bicho HACE, y solo mecánicas que el kit realmente tiene.
            Boss("enemy.artillery",
                "Artillería", "Artillery",
                "Obús de la casa: bombardea de lejos y deja el piso ardiendo donde cae el tiro.",
                "House howitzer: shells from afar and leaves the floor burning where the shot lands.");
            Boss("enemy.charger",
                "Embestidor", "Charger",
                "Se alinea, marca el pasillo y embiste. No te quedes en su línea.",
                "Lines up, marks the lane and charges. Don't stand in its path.");
            Boss("enemy.guardian",
                "Guardián", "Guardian",
                "Protege a los suyos: los aliados cercanos reciben menos daño mientras siga en pie.",
                "Shields its own: nearby allies take less damage while it stands.");
            Boss("enemy.skirmisher",
                "Escaramuzador", "Skirmisher",
                "Tirador inquieto: dispara de lejos y se reacomoda en diagonal para que no lo arrincones.",
                "Restless shooter: fires from range and slips away diagonally so you can't corner it.");
            Boss("enemy.sniper",
                "Francotirador", "Sniper",
                "Dispara desde la otra punta de la sala. Cortale la línea de visión o pagá el tiro.",
                "Fires from across the room. Break its line of sight or pay for the shot.");
            Boss("CardEnemySweeper",
                "Carta Barredora", "Sweeper Card",
                "Soldado de la casa que marca un barrido: su golpe cubre varias casillas de una.",
                "House soldier that marks a sweep: its blow covers several tiles at once.");

            // Bosses del pool nuevo + el rodillo de La Bandida. Mismo criterio que los
            // tres de arriba: texto de pergamino de hover, conciso y mecánico.
            Boss("boss.one_armed",
                "La Bandida", "The One-Armed Bandit",
                "Una tragamonedas de tres rodillos atornillada a la pared. No te persigue: " +
                "cuenta hacia el jackpot. Rompé cualquier rodillo para cancelar la cuenta.",
                "A three-reel slot machine bolted to the wall. It never chases you: " +
                "it counts to the jackpot. Break any reel to cancel the count.");
            Boss("obj.reel",
                "Rodillo", "Reel",
                "Uno de los tres rodillos de La Bandida. Cualquier golpe cancela la cuenta del " +
                "jackpot, pero romper uno cuesta casi todo un turno — y la casilla que deja arde.",
                "One of La Bandida's three reels. Any hit cancels the jackpot count, but " +
                "breaking one costs most of a turn — and the tile it leaves behind burns.");
            Boss("boss.scorekeeper",
                "El Anotador", "The Scorekeeper",
                "El que lleva la planilla. No juega contra vos: te corrige el puntaje " +
                "mientras tirás, y nunca a tu favor.",
                "The one who keeps the sheet. He doesn't play against you: he corrects " +
                "your score as you roll — and never in your favor.");
            Boss("boss.tahur",
                "El Tahúr", "The Cardsharp",
                "La banca canta tu mano antes de que la juegues. Armarla exacta paga; " +
                "pasarse es codicia — y el pozo lleva la cuenta de la codicia.",
                "The bank calls your hand before you play it. Building it exactly pays " +
                "out; overshooting is greed — and the pot keeps count of greed.");
        }

        // ==================================================================
        // Ítems pasivos
        // ==================================================================

        /// <summary>
        /// Los ítems creados por la tool de items entraban a la tabla con ES en las dos
        /// columnas (o EN vacío) — deuda que los tests de tabla venían marcando. Acá
        /// quedan absorbidos con su EN real, y de paso los typos de ES (trailing spaces,
        /// "resvolver", puntos sueltos) se normalizan. Fuente de verdad: este método.
        /// </summary>
        private static void SeedPassiveItems()
        {
            // Movimiento.
            Item("botas.del.viento", "Botas del Viento", "Wind Boots",
                "+2 de movimiento base.", "+2 base movement.");
            Item("botas.del.rayo", "Botas del Rayo", "Lightning Boots",
                "+3 de movimiento base.", "+3 base movement.");
            Item("alas.de.hermes", "Alas de Hermes", "Wings of Hermes",
                "+4 de movimiento base.", "+4 base movement.");

            // Vida máxima.
            Item("coraza.de.hierro", "Coraza de Hierro", "Iron Plate",
                "+35 de vida máxima.", "+35 max health.");
            Item("coraza.templada", "Coraza Templada", "Tempered Plate",
                "+45 de vida máxima.", "+45 max health.");
            Item("coraza.del.titan", "Coraza del Titán", "Titan's Plate",
                "+50 de vida máxima.", "+50 max health.");

            // Curación / sostén.
            Item("talisman.vigoroso", "Talismán Vigoroso", "Vigorous Talisman",
                "Recuperas 10HP al iniciar combate.", "Recover 10 HP when combat starts.");
            Item("regeneracion.de.dados", "Regeneración de Dados", "Dice Regeneration",
                "Recuperas 5HP por cada roll sin utilizar al final del turno.",
                "Recover 5 HP for each unused roll at the end of your turn.");
            Item("Corazón de la Fortuna", "Corazón de la Fortuna", "Heart of Fortune",
                "Recuperas 5HP por cada roll sin utilizar.",
                "Recover 5 HP for each unused roll.");
            Item("ficha.del.segundo.aliento", "Ficha del Segundo Aliento", "Second Wind Chip",
                "La primera vez que el jugador llegaría a 0 HP en la run, queda en 1 HP en vez de morir.",
                "The first time you would hit 0 HP in a run, you stay at 1 HP instead of dying.");

            // Forzar puerta.
            Item("pico.de.minero", "Pico de Minero", "Miner's Pick",
                "+5 a la tirada de forzar puerta.", "+5 to force-door rolls.");
            Item("pico.de.hierro", "Pico de Hierro", "Iron Pick",
                "+12 a la tirada de forzar puerta.", "+12 to force-door rolls.");
            Item("pico.de.diamante", "Pico de Diamante", "Diamond Pick",
                "+20 a la tirada de forzar puerta.", "+20 to force-door rolls.");
            Item("pico.del.demoledor", "Pico del Demoledor", "Wrecker's Pick",
                "+35 a la tirada de forzar puerta.", "+35 to force-door rolls.");

            // Defensa / daño.
            Item("rodilleras.de.obsidiana", "Rodilleras de Obsidiana", "Obsidian Kneepads",
                "+12 en tiradas de defensa.", "+12 to defense rolls.");
            Item("rodilleras.del.bastion", "Rodilleras del Bastión", "Bastion Kneepads",
                "+22 en tiradas de escudo.", "+22 to shield rolls.");
            Item("guantelete.pesado", "Guantelete Pesado", "Heavy Gauntlet",
                "+10 de daño base, a cambio de -1 en tirada de movimiento.",
                "+10 base damage, at the cost of -1 to movement rolls.");
            Item("Guantes.escaladores.escalera", "Guantes del Escalador", "Climber's Gloves",
                "La Escalera hace +25 daño.", "The Straight deals +25 damage.");

            // Utilidad.
            Content("tesoro.de.la.fortuna.name", "Tesoro de la Fortuna", "Treasure of Fortune");
            Item("mochila.grande", "Mochila Grande", "Big Backpack",
                "+1 slot de ítems activos.", "+1 active item slot.");
            Item("llamado.de.emergencia", "Llamado de Emergencia", "Emergency Call",
                "+1 roll de dados permanente.", "+1 permanent dice roll.");

            // Vigías — escudo al resolver un combo. Familia entera, un tier por línea.
            Content("vigia.del.numero.alto.desc",
                "+5 de escudo al resolver el combo Número Alto.",
                "+5 shield when resolving the Higher Number combo.");
            Item("vigia.de.la.cima", "Vigía de la Cima", "Watcher of the Summit",
                "+10 de escudo al resolver el combo Número Alto.",
                "+10 shield when resolving the Higher Number combo.");
            Item("vigia.del.maximo", "Vigía del Máximo", "Watcher of the Maximum",
                "+15 de escudo al resolver el combo Número Alto.",
                "+15 shield when resolving the Higher Number combo.");
            Item("vigia.del.bastion.celestial", "Vigía del Bastión Celestial", "Watcher of the Celestial Bastion",
                "+20 de escudo al resolver el combo Número Alto.",
                "+20 shield when resolving the Higher Number combo.");

            Item("vigia.del.par", "Vigía del Par", "Watcher of the Pair",
                "+5 de escudo al resolver el combo Par.",
                "+5 shield when resolving the Pair combo.");
            Item("vigia.de.los.iguales", "Vigía de los Iguales", "Watcher of the Equals",
                "+10 de escudo al resolver el combo Par.",
                "+10 shield when resolving the Pair combo.");
            Item("vigia.de.los.gemelos", "Vigía de los Gemelos", "Watcher of the Twins",
                "+15 de escudo al resolver el combo Par.",
                "+15 shield when resolving the Pair combo.");
            Item("vigia.de.la.guardia.gemela", "Vigía de la Guardia Gemela", "Watcher of the Twin Guard",
                "+20 de escudo al resolver el combo Par.",
                "+20 shield when resolving the Pair combo.");

            Item("vigia.del.doble.par", "Vigía del Doble Par", "Watcher of the Double Pair",
                "+5 de escudo al resolver el combo Doble Par.",
                "+5 shield when resolving the Double Pair combo.");
            Item("vigia.de.las.parejas", "Vigía de las Parejas", "Watcher of the Pairs",
                "+10 de escudo al resolver el combo Doble Par.",
                "+10 shield when resolving the Double Pair combo.");
            Item("vigia.del.espejo", "Vigía del Espejo", "Watcher of the Mirror",
                "+15 de escudo al resolver el combo Doble Par.",
                "+15 shield when resolving the Double Pair combo.");
            Item("vigia.de.la.contraparte", "Vigía de la Contraparte", "Watcher of the Counterpart",
                "+20 de escudo al resolver el combo Doble Par.",
                "+20 shield when resolving the Double Pair combo.");

            Item("vigia.del.trio", "Vigía del Trío", "Watcher of the Trio",
                "+5 de escudo al resolver el combo Trío.",
                "+5 shield when resolving the Trio combo.");
            Item("vigia.del.triple", "Vigía del Triple", "Watcher of the Triple",
                "+10 de escudo al resolver el combo Trío.",
                "+10 shield when resolving the Trio combo.");
            Item("vigia.de.la.trinidad", "Vigía de la Trinidad", "Watcher of the Trinity",
                "+15 de escudo al resolver el combo Trío.",
                "+15 shield when resolving the Trio combo.");
            Item("vigia.de.los.tres.guardianes", "Vigía de los Tres Guardianes", "Watcher of the Three Guardians",
                "+20 de escudo por cada Trío.",
                "+20 shield for each Trio.");

            Item("vigia.del.full.house", "Vigía del Full House", "Watcher of the Full House",
                "+5 de escudo al resolver el combo Full House.",
                "+5 shield when resolving the Full House combo.");
            Item("vigia.de.la.familia", "Vigía de la Familia", "Watcher of the Family",
                "+10 de escudo al resolver el combo Full House.",
                "+10 shield when resolving the Full House combo.");
            Item("vigia.del.linaje", "Vigía del Linaje", "Watcher of the Lineage",
                "+15 de escudo al resolver el combo Full House.",
                "+15 shield when resolving the Full House combo.");
            Item("vigia.de.la.casa.real", "Vigía de la Casa Real", "Watcher of the Royal House",
                "+20 de escudo al resolver el combo Full House.",
                "+20 shield when resolving the Full House combo.");

            Item("vigia.de.la.escalera", "Vigía de la Escalera", "Watcher of the Straight",
                "+5 de escudo al resolver el combo Escalera.",
                "+5 shield when resolving the Straight combo.");
            Item("vigia.del.ascenso", "Vigía del Ascenso", "Watcher of the Climb",
                "+10 de escudo al resolver el combo Escalera.",
                "+10 shield when resolving the Straight combo.");
            Item("vigia.del.torbellino", "Vigía del Torbellino", "Watcher of the Whirlwind",
                "+15 de escudo al resolver el combo Escalera.",
                "+15 shield when resolving the Straight combo.");
            Item("vigia.de.la.muralla", "Vigía de la Muralla", "Watcher of the Wall",
                "+20 de escudo al completar el combo Escalera.",
                "+20 shield when completing the Straight combo.");

            Item("vigia.del.poker", "Vigía del Póker", "Watcher of Poker",
                "+5 de escudo al resolver el combo Póker.",
                "+5 shield when resolving the Poker combo.");
            Item("vigia.del.cuarteto", "Vigía del Cuarteto", "Watcher of the Quartet",
                "+10 de escudo al resolver el combo Póker.",
                "+10 shield when resolving the Poker combo.");
            Item("vigia.de.los.cuatro.reyes", "Vigía de los Cuatro Reyes", "Watcher of the Four Kings",
                "+15 de escudo al resolver el combo Póker.",
                "+15 shield when resolving the Poker combo.");
            Item("vigia.de.la.guardia.imperial", "Vigía de la Guardia Imperial", "Watcher of the Imperial Guard",
                "+20 de escudo al resolver el combo Póker.",
                "+20 shield when resolving the Poker combo.");

            Item("vigia.de.la.generala", "Vigía de la Generala", "Watcher of the Generala",
                "+5 de escudo al resolver el combo Generala.",
                "+5 shield when resolving the Generala combo.");
            Item("vigia.de.la.plenitud", "Vigía de la Plenitud", "Watcher of Plenty",
                "+10 de escudo al resolver el combo Generala.",
                "+10 shield when resolving the Generala combo.");
            Item("vigia.del.bastion", "Vigía del Bastión", "Watcher of the Bastion",
                "+15 de escudo al resolver el combo Generala.",
                "+15 shield when resolving the Generala combo.");
            Item("vigia.de.la.fortaleza.divina", "Vigía de la Fortaleza Divina", "Watcher of the Divine Fortress",
                "+20 de escudo al resolver el combo Generala.",
                "+20 shield when resolving the Generala combo.");
        }

        // ==================================================================
        // Tooltips de enemigos desde los SOs
        // ==================================================================

        /// <summary>
        /// Vuelca a la tabla Content los textos de tooltip autorados en los
        /// <c>EnemyDataSO</c> (<c>&lt;id&gt;.name</c> / <c>.desc</c> / <c>.type</c>).
        /// Solo agrega keys FALTANTES — nunca pisa una entry curada a mano acá — y el
        /// EN queda marcado <c>[EN] …</c> como pendiente de traducción (así el par pasa
        /// los tests de tabla: EN no vacío y distinto de ES).
        /// </summary>
        [MenuItem("Rollgeon/Localization/Seed Enemy Tooltips From SOs")]
        public static void SeedEnemyTooltipsFromSOs()
        {
            var collection = UnityEditor.Localization.LocalizationEditorSettings
                .GetStringTableCollection(ContentTable);
            if (collection == null)
            {
                Debug.LogError("[LocalizationContentSeeder] No existe la colección Content.");
                return;
            }

            int added = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var so = AssetDatabase.LoadAssetAtPath<Rollgeon.Entities.EnemyDataSO>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (so == null || string.IsNullOrWhiteSpace(so.EntityId)) continue;

                string id = so.EntityId.Trim();
                added += SeedIfMissing(collection, id + ".name", so.DisplayName);
                added += SeedIfMissing(collection, id + ".desc", so.Description);
                added += SeedIfMissing(collection, id + ".type", so.TooltipType);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[LocalizationContentSeeder] Tooltips de enemigos: {added} key(s) " +
                      "nuevas desde SOs (EN marcado [EN], traducir en el seeder).");
        }

        private static int SeedIfMissing(
            UnityEditor.Localization.StringTableCollection collection, string key, string es)
        {
            if (string.IsNullOrWhiteSpace(es)) return 0;
            if (collection.SharedData.GetEntry(key) != null) return 0;

            Content(key, es, "[EN] " + es);
            return 1;
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static void Ui(string key, string es, string en)
            => LocalizationSetupTools.UpsertEntry(UiTable, key, es, en);

        private static void Content(string key, string es, string en)
            => LocalizationSetupTools.UpsertEntry(ContentTable, key, es, en);

        /// <summary>
        /// Atajo para el par <c>&lt;entityId&gt;.name</c> / <c>.desc</c> de un enemigo. El id va
        /// entero y sin prefijo porque <c>EnemyDataSO.EntityId</c> ya lo trae calificado
        /// (<c>boss.*</c>, <c>minion.*</c>, <c>obj.*</c>), que es la key que arma
        /// <c>LocalizedContent</c>.
        /// </summary>
        private static void Boss(string entityId, string nameEs, string nameEn,
                                 string descEs, string descEn)
        {
            Content($"{entityId}.name", nameEs, nameEn);
            Content($"{entityId}.desc", descEs, descEn);
        }

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
