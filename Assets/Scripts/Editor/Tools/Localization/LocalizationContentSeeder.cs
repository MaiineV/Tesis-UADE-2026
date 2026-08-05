using UnityEditor;
using UnityEngine;
using Rollgeon.Tutorial;

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
            SeedEnchantments();
            SeedUnlockHints();
            SeedMiscContent();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LocalizationContentSeeder] Tablas UI + Content pobladas.");
        }

        // ==================================================================
        // Tutorial (tabla UI)
        // ==================================================================

        /// <remarks>
        /// Los <c>{0}</c> los reemplaza <c>TutorialOverlay.ApplyText</c> con la tecla
        /// viva — tienen que sobrevivir la traducción.
        /// </remarks>
        private static void SeedTutorial()
        {
            Ui(TutorialTextKeys.Movement,
                "Hacé click en una casilla para moverte. Caminá hasta la puerta señalada para salir de la sala.",
                "Click a tile to move. Walk to the highlighted door to leave the room.");

            Ui(TutorialTextKeys.EnemiesIntro,
                "¡Combate! Este es tu enemigo. En el tutorial vos siempre actuás primero. (Click para continuar.)",
                "Combat! This is your enemy. In the tutorial you always act first. (Click to continue.)");

            Ui(TutorialTextKeys.MoveTeach,
                "Tu héroe pelea cuerpo a cuerpo: primero hay que acercarse. Seleccioná MOVER ({0}).",
                "Your hero fights in melee: you need to close the distance first. Select MOVE ({0}).");

            Ui(TutorialTextKeys.MoveTiles,
                "Las casillas iluminadas son a donde llegás este turno. Hacé click en una casilla al lado del enemigo.",
                "The highlighted tiles are where you can reach this turn. Click a tile next to the enemy.");

            Ui(TutorialTextKeys.MoveTooFar,
                "Quedaste lejos del enemigo — y solo podés moverte una vez por turno. " +
                "Apretá FINALIZAR TURNO ({0}) y dejá que se acerque él.",
                "You ended up far from the enemy — and you can only move once per turn. " +
                "Press END TURN ({0}) and let them come to you.");

            Ui(TutorialTextKeys.StatsHp,
                "Esta es tu VIDA: si llega a cero, la run se termina. Cuidala. (Click para continuar.)",
                "This is your HEALTH: if it hits zero, the run is over. Look after it. (Click to continue.)");

            Ui(TutorialTextKeys.StatsEnergy,
                "Esta es tu ENERGÍA: moverte, atacar y re-tirar dados la consumen. Administrala en cada turno. (Click para continuar.)",
                "This is your ENERGY: moving, attacking and rerolling dice all spend it. Manage it every turn. (Click to continue.)");

            Ui(TutorialTextKeys.AttackTeach,
                "Se desbloqueó ATACAR ({0}). Seleccionalo para elegir a quién golpear.",
                "ATTACK ({0}) is unlocked. Select it to choose who to hit.");

            Ui(TutorialTextKeys.TargetTeach,
                "Primero se elige el objetivo: hacé click en el enemigo iluminado en rojo.",
                "First you pick the target: click the enemy highlighted in red.");

            Ui(TutorialTextKeys.ThrowTeach,
                "¡Objetivo marcado! Estos son tus dados: agarralos manteniendo click izquierdo " +
                "y arrojalos con un movimiento rápido del mouse.",
                "Target locked! These are your dice: grab them by holding left click " +
                "and throw them with a quick flick of the mouse.");

            Ui(TutorialTextKeys.DiceTeach,
                "Armá combos de generala (par, trío, escalera...). Hacé click en un dado para bloquearlo " +
                "y conservarlo. ¿Querés re-tirar? Agarrá los dados que quieras cambiar y arrojalos de nuevo " +
                "— tenés hasta 3 tiradas. Cuando te guste el combo, apretá CONFIRMAR.",
                "Build generala combos (pair, trio, straight...). Click a die to lock it " +
                "and keep it. Want to reroll? Grab the dice you want to change and throw them again " +
                "— you get up to 3 rolls. When you like the combo, press CONFIRM.");

            Ui(TutorialTextKeys.DefenseTeach,
                "¡Golpe dado! Te sobraron tiradas del ataque, así que viene la fase de DEFENSA: " +
                "agarrá y arrojá los dados para armar un ESCUDO que absorbe el próximo golpe. " +
                "Bloqueá, re-tirá y CONFIRMÁ tu combo de defensa.",
                "Hit landed! You had rolls left over from the attack, so the DEFENSE phase begins: " +
                "grab and throw the dice to build a SHIELD that absorbs the next hit. " +
                "Lock, reroll and CONFIRM your defense combo.");

            Ui(TutorialTextKeys.EndTurnTeach,
                "¡Golpe completado! Cuando no quieras hacer nada más, apretá " +
                "FINALIZAR TURNO ({0}) para cederle el turno al enemigo.",
                "Attack complete! When there's nothing else you want to do, press " +
                "END TURN ({0}) to hand the turn over to the enemy.");

            Ui(TutorialTextKeys.Combat1Free,
                "¡Así se pelea! Repetí el proceso — moverte, atacar, armar combos — hasta vencer al enemigo. (Click para seguir.)",
                "That's how you fight! Repeat the process — move, attack, build combos — until the enemy is down. (Click to continue.)");

            Ui(TutorialTextKeys.HealUnlocked,
                "¡Te golpearon! Se desbloqueó CURAR ({0}): usala en tu turno cuando te falte vida.",
                "You got hit! HEAL ({0}) is unlocked: use it on your turn when you're low on health.");

            Ui(TutorialTextKeys.HealDice,
                "Curar también usa los dados: agarralos con click sostenido, arrojalos y superá el umbral " +
                "para recuperar vida. Bloqueá los dados altos, re-tirá los demás si hace falta y confirmá.",
                "Healing uses the dice too: grab them with a held click, throw them and beat the threshold " +
                "to recover health. Lock the high dice, reroll the rest if you need to, and confirm.");

            Ui(TutorialTextKeys.GoToC,
                "¡Bien hecho! Seguí por la puerta señalada. La otra puerta está bloqueada — la vas a abrir más adelante.",
                "Well done! Head through the highlighted door. The other one is locked — you'll open it later.");

            Ui(TutorialTextKeys.EscapeTeach,
                "¡Son demasiados! Se desbloqueó FORZAR PUERTA ({0}): usala para escapar por donde viniste. " +
                "Solo funciona si estás al lado de una puerta — y ahora lo estás.",
                "There are too many of them! FORCE DOOR ({0}) is unlocked: use it to escape the way you came. " +
                "It only works if you're next to a door — and right now you are.");

            Ui(TutorialTextKeys.EscapeDice,
                "Forzar la puerta se resuelve con los dados: agarralos, arrojalos y superá el umbral para escapar del combate.",
                "Forcing the door is resolved with the dice: grab them, throw them and beat the threshold to escape the fight.");

            Ui(TutorialTextKeys.Combat2Door,
                "La sala se desbloqueó: entrá y terminá lo que empezaste.",
                "The room is unlocked: go in and finish what you started.");

            Ui(TutorialTextKeys.Combat2,
                "¡Ahora sí! Con la mejora de PAR estás listo: acabá con los dos enemigos. (Click para seguir.)",
                "Now you're ready! With the PAIR upgrade you can take on both enemies. (Click to continue.)");

            Ui(TutorialTextKeys.ShopDoor,
                "¡Escapaste! Se abrió la puerta de la tienda. Entrá: te espera algo que te va a dar la ventaja.",
                "You escaped! The shop door is open. Head in: something that will give you the edge is waiting.");

            Ui(TutorialTextKeys.ShopPedestal,
                "Esta es la tienda. Acercate al pedestal y presioná F para comprar la mejora: tu combo PAR va a hacer +50 de daño.",
                "This is the shop. Walk up to the pedestal and press F to buy the upgrade: your PAIR combo will deal +50 damage.");

            Ui(TutorialTextKeys.ShopPurchased,
                "¡Mejora comprada! Ahora sí estás preparado: volvé por donde viniste y acabá con esos enemigos.",
                "Upgrade bought! Now you're ready: head back the way you came and finish those enemies.");

            Ui(TutorialTextKeys.GoToE,
                "¡Excelente! Pasá a la última sala: la de encantamientos.",
                "Excellent! Move on to the last room: the enchantment room.");

            Ui(TutorialTextKeys.EnchantRoom,
                "Esta es la mesa de encantamientos: mejora uno de tus dados a cambio de oro, por el resto de la run. Acercate al altar y presioná F para abrirla.",
                "This is the enchantment table: it upgrades one of your dice for gold, for the rest of the run. Walk up to the altar and press F to open it.");

            Ui(TutorialTextKeys.EnchantTable,
                "Elegí uno de tus dados, elegí el encantamiento y confirmá: el dado queda mejorado por el resto de la run. " +
                "Cuanto más fuerte es el dado, más huecos de encantamiento tiene. " +
                "Cada encantamiento cuesta oro — el que juntaste alcanza para exactamente UNO. (Click para seguir.)",
                "Pick one of your dice, pick the enchantment and confirm: the die stays upgraded for the rest of the run. " +
                "The stronger the die, the more enchantment slots it has. " +
                "Each enchantment costs gold — what you saved is enough for exactly ONE. (Click to continue.)");

            Ui(TutorialTextKeys.EnchantDone,
                "¡Dado encantado! Ya sabés todo lo que necesitás — cerrá la mesa.",
                "Die enchanted! You know everything you need — close the table.");

            Ui(TutorialTextKeys.Exit,
                "Cruzá la puerta señalada para empezar tu aventura de verdad.",
                "Step through the highlighted door to begin your real adventure.");

            Ui(TutorialTextKeys.ContinueFooter,
                "Hacé click para continuar",
                "Click to continue");
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
                "Si este dado se holdea entre tiradas, su resultado aumenta +1 por tirada holdeada (máx +3).",
                "If this die is held between rolls, its result increases by +1 per held roll (max +3).");

            Ench("avaro", "Avaro", "Miser",
                "Otorga 3 de oro extra al completar un combo de trío o superior.",
                "Grants 3 extra gold when you complete a trio combo or better.");

            Ench("caras_centrales", "Caras Centrales", "Middle Faces",
                "Bloquea el cuarto superior e inferior. d8:{3,4,5,6} d12:{4,5,6,7,8,9}",
                "Blocks the top and bottom quarter. d8:{3,4,5,6} d12:{4,5,6,7,8,9}");

            Ench("cargado", "Cargado", "Loaded",
                "Una vez por combate podés relanzar este dado y quedarte con el mayor resultado.",
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
                "Este dado no se puede holdear entre tiradas.",
                "This die cannot be held between rolls.");

            Ench("mercader", "Mercader", "Merchant",
                "Otorga 5 de oro al completar una escalera.",
                "Grants 5 gold when you complete a straight.");

            Ench("mimetico", "Mimético", "Mimic",
                "Copia el resultado del último dado re-tirado en esta tirada para propósitos de combo.",
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
                "Fuerza un re-tiro completo en el turno 2 del combate.",
                "Forces a full reroll on turn 2 of the combat.");

            Ench("volatil", "Volátil", "Volatile",
                "Al sacar el máximo, el resultado se duplica. Al sacar el mínimo, vale 0.",
                "On a maximum roll the result doubles. On a minimum roll it's worth 0.");
        }

        // ==================================================================
        // Pistas de desbloqueables (tabla Content)
        // ==================================================================

        /// <remarks>
        /// La pista es lo que ve el jugador mientras el unlock está BLOQUEADO — o sea,
        /// lo único visible hasta cumplirlo. La resuelve <c>LocalizedContent.Hint</c>.
        /// </remarks>
        private static void SeedUnlockHints()
        {
            Content("unlock.dice.d8.hint",
                "Dominá el dado estándar: ganá una run confiando solo en el clásico de seis caras.",
                "Master the standard die: win a run relying only on the classic six-sider.");

            Content("unlock.dice.d10.hint",
                "Hay una receta exacta de dados que abre esta puerta. Experimentá con la mezcla.",
                "There's an exact dice recipe that opens this door. Experiment with the mix.");

            Content("unlock.class.berserker.hint",
                "Demostrá fuerza de ocho caras: llevá el dado nuevo a una victoria.",
                "Prove your eight-sided strength: carry the new die to a victory.");

            Content("unlock.class.gambler.hint",
                "Un verdadero apostador no deja ninguna jugada del Contrato sin cobrar.",
                "A true gambler never leaves a Contract play uncashed.");
        }

        // ==================================================================
        // Barrido del resto de Content que seguía sin traducir
        // ==================================================================

        private static void SeedMiscContent()
        {
            // Recompensas de personaje — se veían en inglés incluso en español.
            Content("char_rew.attack_plus_3.name", "Ataque +3", "Attack +3");
            Content("char_rew.energy_plus_1.name", "Energía +1", "Energy +1");
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
    }
}
