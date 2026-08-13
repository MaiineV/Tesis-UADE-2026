using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Catálogo de descripciones por tipo de nodo del AI tree, mostradas en el side panel
    /// del inspector. Cuando un nodo nuevo se agrega al runtime, sumar su entry acá para
    /// que el editor le muestre help al designer.
    /// </summary>
    public static class AINodeDocumentation
    {
        private static readonly Dictionary<Type, string> _descriptions = new Dictionary<Type, string>
        {
            [typeof(AINode_Sequence)] =
                "Secuencia (AND): ejecuta los Children en orden. Si alguno retorna Failed, " +
                "corta ahí y devuelve Failed sin tocar el resto. Si todos retornan Succeeded, " +
                "devuelve Succeeded.\n\n" +
                "Usalo cuando una serie de pasos tiene que pasar todos en orden " +
                "(ej: 'moverte → atacar → recuperar energía').",

            [typeof(AINode_Selector)] =
                "Selector (OR): prueba los Children en orden hasta que uno retorne Succeeded — " +
                "devuelve Succeeded sin tocar los demás. Si todos fallan, devuelve Failed.\n\n" +
                "Usalo para fallback chains: 'intentá X; si no podés, intentá Y; si no, hacé Z'.",

            [typeof(AINode_If)] =
                "If: ramifica según la lista AND-evaluada de Conditions (PreConditions). " +
                "Si todas pasan, ejecuta el branch Then; si alguna falla, ejecuta Else.\n\n" +
                "TargetSelector decide a quién apuntan las Conditions (default: Always Player). " +
                "Lista de Conditions vacía = AND-empty = pasa siempre (toma Then).",

            [typeof(AINode_While)] =
                "While: loop. Re-ejecuta el Body mientras todas las Conditions sean true, " +
                "hasta MaxIterations.\n\n" +
                "• Conditions vacías → permisivo (true) → MaxIterations es el único corte.\n" +
                "• Body retorna Failed → propaga Failed inmediatamente.\n" +
                "• Cap alcanzado → Failed + warning log (señal de bug — condition que nunca flips).",

            [typeof(AINode_Random)] =
                "Random (weighted): elige un hijo al azar entre los Options usando Weight " +
                "como probabilidad relativa (mayor weight = más probable). Devuelve el " +
                "resultado del child elegido.\n\n" +
                "RNG inyectado vía AIContext.Rng — los tests pueden seedearlo para determinismo.",

            [typeof(AINode_Alternate)] =
                "Alternate (round-robin): rota entre los Children en orden fijo, uno por " +
                "turno (0, 1, 2, ..., 0, 1, 2, ...). A diferencia de Random, garantiza que " +
                "nunca se repite el mismo hijo dos veces seguidas.\n\n" +
                "Usalo cuando necesitás que el enemigo alterne dos comportamientos (ej. " +
                "'ataque especial' / 'golpe normal') sin depender del azar. El índice vive " +
                "en la instancia runtime (se resetea a 0 en cada pelea nueva).",

            [typeof(AINode_Move)] =
                "Move Toward Target: mueve al enemy hasta MaxSteps casillas (AIIntReader) hacia " +
                "el target del TargetSelector (null = player), manteniendo DesiredRange casillas " +
                "de distancia Manhattan. Si DesiredRange es null usa el legacy StopAdjacent " +
                "(true → 1, false → 0).\n\n" +
                "Si Retreat es true y está más cerca que DesiredRange, retrocede (kite); si es " +
                "false, demasiado cerca = no se mueve. Devuelve Succeeded si se movió, Failed si " +
                "ya está en la banda o no hay tile mejor. Setea AIContext.PendingWait con la animación.",

            [typeof(AINode_KeepDistance)] =
                "Keep Distance (kiting): mueve al enemy hasta MaxSteps casillas para mantener " +
                "IdealDistance del player. Si ya está a la distancia ideal, no se mueve.\n\n" +
                "Usalo para enemigos ranged/casters que evitan el melee.",

            [typeof(AINode_Wait)] =
                "Wait: no-op. Siempre devuelve Succeeded sin hacer nada.\n\n" +
                "Usalo como placeholder durante prototipado, o como pad de turno cuando un " +
                "branch no debería actuar pero tampoco fallar.",

            [typeof(AINode_TelegraphMark)] =
                "Telegraph Mark: marca un área (sin dañar este turno) para que " +
                "ExecuteTelegraph la detone el turno siguiente del enemigo.\n\n" +
                "Shape=DirectionalBand: el área sale del propio enemigo (no del jugador) — " +
                "banda perpendicular de 2·Size+1 casillas, centrada en su coordenada, " +
                "extendida Depth casillas en la dirección cardinal dominante hacia el " +
                "jugador.\n\n" +
                "Shape=ScatteredSquares: Count cuadrados de Size×Size, anclados al azar " +
                "(AIContext.Rng) en el 50% central de la sala — ni el jugador ni el " +
                "enemigo son el centro, y nunca aparecen pegados a las paredes. Prioriza " +
                "que los cuadrados no se toquen ni se solapen entre sí (degrada el gap si " +
                "la sala no da lugar). Requiere una sala con bounds reales.\n\n" +
                "Shape=SquareAroundSelf: mismo cálculo que Square (2·Size+1), pero centrado " +
                "en la coordenada del propio enemigo en vez de la del jugador.\n\n" +
                "Las demás shapes (Square/Row/Column/HalfRoom) se centran en el jugador " +
                "como siempre.",

            [typeof(AINode_Behavior)] =
                "Behavior: ejecuta un EnemyActionBehavior — la unidad reusable de combate " +
                "(ataque, heal, buff, etc.).\n\n" +
                "El TargetSelector del Behavior resuelve a quién apuntan los Effects. Cada " +
                "Effect tiene su propia lista de PreConditions independiente — el behavior " +
                "ejecuta solo los Effects cuyas PreConditions pasan.",

            [typeof(AINode_ActivateRainHazard)] =
                "Activate Rain Hazard: activa RainHazardService (idempotente) — una amenaza " +
                "ambiental independiente del boss (fuente propia) que, una vez activa, marca " +
                "y detona zonas erráticas en su propio ciclo de rondas, en paralelo a lo que " +
                "esté haciendo el boss.\n\n" +
                "Pensado para envolver en If(PcOwnerHpBelow) → Once(...), igual que el " +
                "trigger de refuerzos — dispara una sola vez al cruzar el umbral de HP.",

            [typeof(AINode_SpawnReinforcements)] =
                "Spawn Reinforcements: spawnea Count copias de EnemyToSpawn en tiles del " +
                "borde de la sala (perímetro del bounding box, walkable y libres) y los " +
                "suma a la ronda de combate en curso.\n\n" +
                "Los refuerzos van al FINAL de la ronda actual — quien ya estaba en cola " +
                "(player/boss) termina su turno normal antes; de ahí en adelante rotan de " +
                "forma regular y estable, como cualquier otro participante.\n\n" +
                "Pensado para envolver en If(PcOwnerHpBelow) → Once(...) — dispara una sola " +
                "vez al cruzar el umbral de HP, igual que otros triggers de fase.",

            // ---- La Generala (piso 3) — mano de dados propia -------------------------

            [typeof(AINode_RollHand)] =
                "Roll Hand: tira la mano de dados del propio boss y la corre por el MISMO " +
                "detector de combos que usa el jugador (ComboResolver + ComboCatalogSO). " +
                "Publica el resultado (caras + combo) en IBossDiceHandService; las ramas de " +
                "ataque lo leen con la precondición PcBossHandCombo.\n\n" +
                "SizeSource=AliveAllies tira tantos dados como aliados vivos tenga el boss — " +
                "sus dados SON sus aliados (objetos en el piso), así que romperle uno le borra " +
                "una categoría gratis: Generala pide 5 dados en la tirada, Póker 4. NO metas " +
                "otros enemigos en la arena o van a contar como dados. Fixed = siempre MaxDice.\n\n" +
                "SlowCombos: esos combos se publican 'cantados pero no armados' — ese turno " +
                "nadie marca y el siguiente el nodo los arma SIN re-tirar. Eso es la ronda " +
                "extra de aviso (2 rondas entre la tirada y el impacto).\n\n" +
                "Con rerolls habilitados (AINode_SetHandReroll) re-tira los dados que no " +
                "contribuyen al combo y se queda con la mejor mano por prioridad.",

            [typeof(AINode_SetHandReroll)] =
                "Set Hand Reroll: habilita (o saca) el reroll de la mano de dados del boss — " +
                "cuántas veces re-tira por tirada los dados que no le sirven.\n\n" +
                "Setup de Fase 2: metelo en If(PcOwnerHpBelow) → Once(...). El flag vive en el " +
                "servicio de la mano (run-scoped), así que aplicarlo una vez alcanza.",

            [typeof(AINode_AdoptWeakness)] =
                "Adopt Weakness: le reasigna la debilidad al propio boss al combo que el " +
                "jugador MÁS viene usando — lee IComboLogService y escribe IWeaknessRegistry.\n\n" +
                "Empates: gana el más reciente. El marcador de 'sin combo' del log se ignora. " +
                "Con el log vacío devuelve Succeeded y deja la debilidad como estaba (poné " +
                "FailWhenLogEmpty si preferís que falle).\n\n" +
                "Setup de Fase 2: If(PcOwnerHpBelow) → Once(...).",

            [typeof(AINode_AuxTelegraph)] =
                "Aux Telegraph: telegraph de canal SECUNDARIO. Misma semántica que " +
                "TelegraphMark + ExecuteTelegraph (marco en el turno N, cobro en el N+1), pero " +
                "bajo una fuente propia derivada de ChannelId, así que NO se pisa con el " +
                "telegraph principal del boss.\n\n" +
                "Existe porque IThreatenedAreaService guarda UN área pendiente por fuente y " +
                "Mark sobrescribe la anterior: un boss que amenaza dos áreas el mismo turno " +
                "perdería una.\n\n" +
                "Se cablea de a dos instancias con el MISMO ChannelId: una en Step=Execute " +
                "arriba del Sequence (al lado del ExecuteTelegraph principal y FUERA de todo " +
                "gate — el aviso hay que cobrarlo aunque este turno no se marque de nuevo) y " +
                "una en Step=Mark donde corresponda.\n\n" +
                "Shapes soportadas: las centradas (SquareAroundSelf, SquareAroundPlayer, Row, " +
                "Column, HalfRoom). DirectionalBand y ScatteredSquares no — usá el nodo principal.",
        };

        /// <summary>
        /// Devuelve la descripción para el tipo de nodo, o <c>null</c> si no hay registrada.
        /// </summary>
        public static string Get(Type t)
        {
            if (t == null) return null;
            return _descriptions.TryGetValue(t, out var doc) ? doc : null;
        }
    }
}
